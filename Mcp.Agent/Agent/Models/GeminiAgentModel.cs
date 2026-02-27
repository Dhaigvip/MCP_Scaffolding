using Mcp.Agent.ModelAbstraction;
using System.Text;
using System.Text.Json;

namespace Mcp.Agent.Models;

public sealed class GeminiAgentModel : IAgentModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GeminiAgentModel(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public async Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? "gemini-2.0-flash" : request.Model;

        var payload = new Dictionary<string, object?>
        {
            ["contents"] = BuildGeminiContents(request.SystemPrompt, request.Messages),
            ["tools"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["functionDeclarations"] = request.Tools.Select(ToGeminiFunctionDeclaration).ToList()
                }
            }
        };

        var url = $"models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

        using var reqMsg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini error {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var parts = new List<AgentMessagePart>();
        var hasToolCalls = false;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return new AgentModelResponse { Parts = parts, HasToolCalls = false };

        var content = candidates[0].GetProperty("content");
        var respParts = content.GetProperty("parts");

        foreach (var p in respParts.EnumerateArray())
        {
            if (p.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(AgentMessagePart.TextPart(text));
                continue;
            }

            if (p.TryGetProperty("functionCall", out var fc))
            {
                hasToolCalls = true;

                var name = fc.GetProperty("name").GetString() ?? "";
                var argsEl = fc.TryGetProperty("args", out var argsObj) ? argsObj : default;

                var argsDict = new Dictionary<string, JsonElement>();
                if (argsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in argsEl.EnumerateObject())
                        argsDict[prop.Name] = prop.Value.Clone();
                }

                var callId = Guid.NewGuid().ToString("N");
                parts.Add(AgentMessagePart.ToolCallPart(callId, name, argsDict));
            }
        }

        return new AgentModelResponse
        {
            Parts = parts,
            HasToolCalls = hasToolCalls
        };
    }

    private static List<Dictionary<string, object?>> BuildGeminiContents(
        string systemPrompt,
        IReadOnlyList<AgentMessage> history)
    {
        var contents = new List<Dictionary<string, object?>>();

        contents.Add(new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["parts"] = new object[]
            {
                new Dictionary<string, object?> { ["text"] = systemPrompt }
            }
        });

        foreach (var msg in history)
        {
            if (msg.Role == AgentRole.User)
            {
                var userText = string.Join("", msg.Parts.Where(p => p.Type == AgentPartType.Text).Select(p => p.Text ?? ""));
                if (!string.IsNullOrWhiteSpace(userText))
                {
                    contents.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = userText } }
                    });
                }

                foreach (var tr in msg.Parts.Where(p => p.Type == AgentPartType.ToolResult))
                {
                    var callId = tr.ToolCallId ?? "";
                    var content = tr.ToolResultContent ?? "";
                    var toolName = InferToolNameFromToolResultPlaceholder(tr) ?? "tool";

                    contents.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["parts"] = new object[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["functionResponse"] = new Dictionary<string, object?>
                                {
                                    ["name"] = toolName,
                                    ["response"] = new Dictionary<string, object?>
                                    {
                                        ["content"] = content,
                                        ["callId"] = callId,
                                        ["isError"] = tr.ToolResultIsError
                                    }
                                }
                            }
                        }
                    });
                }

                continue;
            }

            var assistantText = string.Join("", msg.Parts.Where(p => p.Type == AgentPartType.Text).Select(p => p.Text ?? ""));
            var assistantParts = new List<Dictionary<string, object?>>();

            if (!string.IsNullOrWhiteSpace(assistantText))
                assistantParts.Add(new Dictionary<string, object?> { ["text"] = assistantText });

            foreach (var tc in msg.Parts.Where(p => p.Type == AgentPartType.ToolCall))
            {
                assistantParts.Add(new Dictionary<string, object?>
                {
                    ["functionCall"] = new Dictionary<string, object?>
                    {
                        ["name"] = tc.ToolName,
                        ["args"] = tc.ToolInput
                    }
                });
            }

            if (assistantParts.Count > 0)
            {
                contents.Add(new Dictionary<string, object?>
                {
                    ["role"] = "model",
                    ["parts"] = assistantParts
                });
            }
        }

        return contents;
    }

    private static object ToGeminiFunctionDeclaration(AgentTool tool)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["parameters"] = JsonSerializer.Deserialize<object>(tool.InputJsonSchema.GetRawText())
        };
    }

    private static string? InferToolNameFromToolResultPlaceholder(AgentMessagePart toolResultPart)
    {
        return toolResultPart.ToolName;
    }
}