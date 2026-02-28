using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Mcp.Agent.ModelAbstraction;

namespace Mcp.Agent.Models;

public sealed class AnthropicAgentModel : IAgentModel
{
    private readonly AnthropicClient _client;

    public AnthropicAgentModel(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<AgentModelResponse> GenerateAsync(
        AgentModelRequest request,
        CancellationToken ct)
    {
        var tools = request.Tools.Select(ToAnthropicTool).ToList();
        var messages = request.Messages.Select(ToAnthropicMessageParam).ToList();

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = request.Model is null ? Model.ClaudeOpus4_6 : request.Model,
            MaxTokens = request.MaxTokens,
            Thinking = new ThinkingConfigAdaptive(),
            Tools = tools,
            Messages = messages,
            System = request.SystemPrompt
        }, ct);

        var parts = new List<AgentMessagePart>();
        var hasToolCalls = false;

        foreach (var block in response.Content)
        {
            if (block.TryPickThinking(out var thinkingBlock))
            {
                if (!string.IsNullOrWhiteSpace(thinkingBlock.Thinking))
                    parts.Add(AgentMessagePart.ThinkingPart(thinkingBlock.Thinking));
            }
            else if (block.TryPickText(out var textBlock))
            {
                if (!string.IsNullOrWhiteSpace(textBlock.Text))
                    parts.Add(AgentMessagePart.TextPart(textBlock.Text));
            }
            else if (block.TryPickToolUse(out var toolUse))
            {
                hasToolCalls = true;
                parts.Add(AgentMessagePart.ToolCallPart(toolUse.ID, toolUse.Name, toolUse.Input));
            }
        }

        var stopIsToolUse = response.StopReason!.Raw() == "tool_use";
        hasToolCalls = hasToolCalls || stopIsToolUse;

        return new AgentModelResponse
        {
            Parts = parts,
            HasToolCalls = hasToolCalls
        };
    }

    private static ToolUnion ToAnthropicTool(AgentTool tool)
    {
        var schemaRaw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            tool.InputJsonSchema.GetRawText());

        if (schemaRaw is null)
            throw new InvalidOperationException($"Invalid JSON schema for tool '{tool.Name}'.");

        ToolUnion t = new Tool
        {
            Name = tool.Name,
            Description = tool.Description,
            InputSchema = new InputSchema(schemaRaw)
        };

        return t;
    }

    private static MessageParam ToAnthropicMessageParam(AgentMessage message)
    {
        if (message.Role == AgentRole.User)
            return ToAnthropicUserMessage(message);

        return ToAnthropicAssistantMessage(message);
    }

    private static MessageParam ToAnthropicUserMessage(AgentMessage message)
    {
        var toolResults = message.Parts.Where(p => p.Type == AgentPartType.ToolResult).ToList();
        var texts = message.Parts.Where(p => p.Type == AgentPartType.Text).ToList();

        if (toolResults.Count == 0 && texts.Count == 1)
        {
            return new MessageParam
            {
                Role = Role.User,
                Content = texts[0].Text ?? ""
            };
        }

        var blocks = new List<ContentBlockParam>();

        foreach (var text in texts)
        {
            var textJson = JsonSerializer.SerializeToElement(new
            {
                type = "text",
                text = text.Text ?? ""
            });
            blocks.Add(new ContentBlockParam(textJson));
        }

        foreach (var tr in toolResults)
        {
            var callId = tr.ToolCallId ?? throw new InvalidOperationException("ToolResult missing call id.");
            var content = tr.ToolResultContent ?? "";

            blocks.Add(new ToolResultBlockParam(callId)
            {
                Content = new ToolResultBlockParamContent(content, null),
                IsError = tr.ToolResultIsError
            });
        }

        return new MessageParam
        {
            Role = Role.User,
            Content = blocks
        };
    }

    private static MessageParam ToAnthropicAssistantMessage(AgentMessage message)
    {
        var blocks = new List<ContentBlockParam>();

        foreach (var part in message.Parts)
        {
            if (part.Type == AgentPartType.Text)
            {
                var textJson = JsonSerializer.SerializeToElement(new
                {
                    type = "text",
                    text = part.Text ?? ""
                });
                blocks.Add(new ContentBlockParam(textJson));
                continue;
            }

            if (part.Type == AgentPartType.Thinking)
            {
                var thinkingJson = JsonSerializer.SerializeToElement(new
                {
                    type = "thinking",
                    thinking = part.Thinking ?? ""
                });
                blocks.Add(new ContentBlockParam(thinkingJson));
                continue;
            }

            if (part.Type == AgentPartType.ToolCall)
            {
                var callId = part.ToolCallId ?? throw new InvalidOperationException("ToolCall missing id.");
                var toolName = part.ToolName ?? throw new InvalidOperationException("ToolCall missing name.");
                var input = part.ToolInput ?? new Dictionary<string, JsonElement>();

                var toolUseJson = JsonSerializer.SerializeToElement(new
                {
                    type = "tool_use",
                    id = callId,
                    name = toolName,
                    input
                });
                blocks.Add(new ContentBlockParam(toolUseJson));
                continue;
            }
        }

        return new MessageParam
        {
            Role = Role.Assistant,
            Content = blocks
        };
    }
}