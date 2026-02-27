using System.Text.Json.Serialization;
using Mcp.Governance.Policy;

namespace Mcp.Agent;

// ─── Risk info (sent to React so it can render the approval dialog) ───────────

public sealed record RiskInfo(
    string Level,       // "high" | "medium" | "low" | "readonly"
    string Label,
    string Color,       // "red" | "yellow" | "green" | "blue"
    string Emoji,
    bool RequiresApproval,
    bool DefaultApprove,
    string? Warning = null)
{
    public static RiskInfo FromRiskLevel(RiskLevel level) => level switch
    {
        RiskLevel.High => new("high", "HIGH RISK", "red", "🔴",
            RequiresApproval: true, DefaultApprove: false,
            Warning: "⚠️  This is a DESTRUCTIVE operation that may be irreversible."),

        RiskLevel.Medium => new("medium", "MODERATE RISK", "yellow", "🟡",
            RequiresApproval: true, DefaultApprove: true),

        RiskLevel.Low => new("low", "LOW RISK", "green", "🟢",
            RequiresApproval: false, DefaultApprove: true),

        RiskLevel.ReadOnly => new("readonly", "READ ONLY", "blue", "🔵",
            RequiresApproval: false, DefaultApprove: true),

        _ => new("medium", "MODERATE RISK", "yellow", "🟡",
            RequiresApproval: true, DefaultApprove: true),
    };
}

// ─── Polymorphic event hierarchy ──────────────────────────────────────────────
// The "type" discriminator matches the TypeScript/React side exactly.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThinkingAgentEvent), "thinking")]
[JsonDerivedType(typeof(StatusAgentEvent), "status")]
[JsonDerivedType(typeof(TextDeltaAgentEvent), "text_delta")]
[JsonDerivedType(typeof(TextEndAgentEvent), "text_end")]
[JsonDerivedType(typeof(HitlAgentEvent), "hitl")]
[JsonDerivedType(typeof(ToolAutoAgentEvent), "tool_auto")]
[JsonDerivedType(typeof(ToolResultAgentEvent), "tool_result")]
[JsonDerivedType(typeof(ToolSkippedAgentEvent), "tool_skipped")]
[JsonDerivedType(typeof(DoneAgentEvent), "done")]
[JsonDerivedType(typeof(ErrorAgentEvent), "error")]
public abstract record AgentEvent
{
    public abstract string Type { get; }
}

/// <summary>Claude's full internal reasoning — shown as a collapsible block in the UI.</summary>
public sealed record ThinkingAgentEvent(string Content) : AgentEvent
{
    public override string Type => "thinking";
}

/// <summary>
/// Brief status message shown while Claude is working (e.g. "Planning next step…").
/// Lighter-weight alternative to the full ThinkingAgentEvent.
/// </summary>
public sealed record StatusAgentEvent(string Message) : AgentEvent
{
    public override string Type => "status";
}

/// <summary>A token of the assistant's streamed text response.</summary>
public sealed record TextDeltaAgentEvent(string Delta) : AgentEvent
{
    public override string Type => "text_delta";
}


/// <summary>Current text block is complete.</summary>
public sealed record TextEndAgentEvent() : AgentEvent
{
    public override string Type => "text_end";
}

/// <summary>
/// Agent wants to call a risky tool and is PAUSED waiting for user approval.
/// React must show an approval dialog and POST to /api/agent/approve.
/// </summary>
public sealed record HitlAgentEvent(
    string CallId,
    string ToolName,
    object Input,
    RiskInfo Risk) : AgentEvent
{
    public override string Type => "hitl";
}

/// <summary>A low-risk tool is being executed automatically (no approval needed).</summary>
public sealed record ToolAutoAgentEvent(
    string ToolName,
    object Input) : AgentEvent
{
    public override string Type => "tool_auto";
}

/// <summary>Tool finished — display result to user.</summary>
public sealed record ToolResultAgentEvent(
    string ToolName,
    string Content,
    bool IsError) : AgentEvent
{
    public override string Type => "tool_result";
}

/// <summary>User declined a tool call.</summary>
public sealed record ToolSkippedAgentEvent(string ToolName) : AgentEvent
{
    public override string Type => "tool_skipped";
}

/// <summary>Agent turn is fully complete.</summary>
public sealed record DoneAgentEvent() : AgentEvent
{
    public override string Type => "done";
}

/// <summary>Something went wrong.</summary>
public sealed record ErrorAgentEvent(string Message) : AgentEvent
{
    public override string Type => "error";
}
