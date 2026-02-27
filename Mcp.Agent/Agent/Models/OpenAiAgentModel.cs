using Mcp.Agent.ModelAbstraction;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Mcp.Agent.Models;

public sealed class OpenAiAgentModel : IAgentModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;

    public OpenAiAgentModel(HttpClient http)
    {
        _http = http;
    }

    public async Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = string.IsNullOrWhiteSpace(request.Model) ? "gpt-4.1" : request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["messages"] = BuildOpenAiMessages(request.SystemPrompt, request.Messages),
            ["tools"] = request.Tools.Select(ToOpenAiTool).ToList(),
            ["tool_choice"] = "auto"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI error {(int)resp.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var choice0 = root.GetProperty("choices")[0];
        var message = choice0.GetProperty("message");

        var parts = new List<AgentMessagePart>();
        var hasToolCalls = false;

        if (message.TryGetProperty("content", out var contentEl) &&
            contentEl.ValueKind == JsonValueKind.String)
        {
            var text = contentEl.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(AgentMessagePart.TextPart(text));
        }

        if (message.TryGetProperty("tool_calls", out var toolCallsEl) &&
            toolCallsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in toolCallsEl.EnumerateArray())
            {
                hasToolCalls = true;

                var id = tc.GetProperty("id").GetString() ?? "";
                var fn = tc.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? "";
                var argsJson = fn.GetProperty("arguments").GetString() ?? "{}";

                var argsDict = ParseJsonObjectToDictionary(argsJson);

                parts.Add(AgentMessagePart.ToolCallPart(id, name, argsDict));
            }
        }

        return new AgentModelResponse
        {
            Parts = parts,
            HasToolCalls = hasToolCalls
        };
    }

    private static List<Dictionary<string, object?>> BuildOpenAiMessages(
        string systemPrompt,
        IReadOnlyList<AgentMessage> history)
    {
        var list = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["role"] = "system",
                ["content"] = systemPrompt
            }
        };

        foreach (var msg in history)
        {
            if (msg.Role == AgentRole.User)
            {
                foreach (var group in SplitUserParts(msg.Parts))
                    list.Add(group);
            }
            else
            {
                list.Add(ToAssistantMessage(msg.Parts));
            }
        }

        return list;
    }

    private static IEnumerable<Dictionary<string, object?>> SplitUserParts(List<AgentMessagePart> parts)
    {
        var textParts = parts.Where(p => p.Type == AgentPartType.Text && !string.IsNullOrWhiteSpace(p.Text)).ToList();
        var toolResults = parts.Where(p => p.Type == AgentPartType.ToolResult).ToList();

        if (textParts.Count == 1 && toolResults.Count == 0)
        {
            yield return new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = textParts[0].Text
            };
            yield break;
        }

        foreach (var t in textParts)
        {
            yield return new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = t.Text
            };
        }

        foreach (var tr in toolResults)
        {
            var callId = tr.ToolCallId ?? throw new InvalidOperationException("ToolResult missing tool_call_id.");
            var content = tr.ToolResultContent ?? "";

            yield return new Dictionary<string, object?>
            {
                ["role"] = "tool",
                ["tool_call_id"] = callId,
                ["content"] = content
            };
        }
    }

    private static Dictionary<string, object?> ToAssistantMessage(List<AgentMessagePart> parts)
    {
        var msg = new Dictionary<string, object?>
        {
            ["role"] = "assistant"
        };

        var text = string.Join("", parts.Where(p => p.Type == AgentPartType.Text).Select(p => p.Text ?? ""));
        if (!string.IsNullOrWhiteSpace(text))
            msg["content"] = text;

        var toolCalls = parts.Where(p => p.Type == AgentPartType.ToolCall).ToList();
        if (toolCalls.Count > 0)
        {
            msg["tool_calls"] = toolCalls.Select(tc =>
            {
                var args = tc.ToolInput ?? new Dictionary<string, JsonElement>();
                return new Dictionary<string, object?>
                {
                    ["id"] = tc.ToolCallId,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = tc.ToolName,
                        ["arguments"] = JsonSerializer.Serialize(args)
                    }
                };
            }).ToList();
        }

        if (!msg.ContainsKey("content"))
            msg["content"] = "";

        return msg;
    }

    private static object ToOpenAiTool(AgentTool tool)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = JsonSerializer.Deserialize<object>(tool.InputJsonSchema.GetRawText())
            }
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseJsonObjectToDictionary(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, JsonElement>();

        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();

        return dict;
    }
}