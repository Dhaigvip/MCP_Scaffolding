
using System.Text.Json;


namespace Mcp.Agent.ModelAbstraction;
public sealed class AgentModelRequest
{
    public string SystemPrompt { get; init; } = "";
    public string? Model { get; init; }
    public int MaxTokens { get; init; } = 4096;

    public IReadOnlyList<AgentTool> Tools { get; init; } = Array.Empty<AgentTool>();
    public IReadOnlyList<AgentMessage> Messages { get; init; } = Array.Empty<AgentMessage>();
}

public sealed class AgentTool
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public JsonElement InputJsonSchema { get; init; }
}

public sealed class AgentMessage
{
    public AgentRole Role { get; init; }
    public List<AgentMessagePart> Parts { get; init; } = new();
}

public enum AgentRole
{
    User = 1,
    Assistant = 2
}

public enum AgentPartType
{
    Text = 1,
    ToolCall = 2,
    ToolResult = 3,
    Thinking = 4
}

public sealed class AgentMessagePart
{
    public AgentPartType Type { get; init; }

    public string? Text { get; init; }

    public string? ToolCallId { get; init; }
    public string? ToolName { get; init; }
    public IReadOnlyDictionary<string, JsonElement>? ToolInput { get; init; }

    public string? ToolResultContent { get; init; }
    public bool ToolResultIsError { get; init; }

    public string? Thinking { get; init; }

    public static AgentMessagePart TextPart(string text) =>
        new AgentMessagePart { Type = AgentPartType.Text, Text = text };

    public static AgentMessagePart ThinkingPart(string thinking) =>
        new AgentMessagePart { Type = AgentPartType.Thinking, Thinking = thinking };

    public static AgentMessagePart ToolCallPart(string callId, string toolName, IReadOnlyDictionary<string, JsonElement> input) =>
        new AgentMessagePart
        {
            Type = AgentPartType.ToolCall,
            ToolCallId = callId,
            ToolName = toolName,
            ToolInput = input
        };

    public static AgentMessagePart ToolResultPart(string callId, string content, bool isError) =>
        new AgentMessagePart
        {
            Type = AgentPartType.ToolResult,
            ToolCallId = callId,
            ToolResultContent = content,
            ToolResultIsError = isError
        };
}

public sealed class AgentModelResponse
{
    public List<AgentMessagePart> Parts { get; init; } = new();
    public bool HasToolCalls { get; init; }
}