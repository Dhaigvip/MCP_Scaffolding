namespace Mcp.Governance.Execution;

public sealed class GovernanceException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    public GovernanceException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }

    public static GovernanceException NotFound(string toolName)
        => new("tool_not_exposed", $"Tool is not exposed: {toolName}", new { toolName });

    public static GovernanceException Forbidden(string toolName, string code, string message)
        => new(code, message, new { toolName });
}