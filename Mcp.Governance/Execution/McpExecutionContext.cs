using System.Security.Claims;

namespace Mcp.Governance.Execution;

public sealed class McpExecutionContext
{
    public required string ToolName { get; init; }
    public required string CorrelationId { get; init; }
    public required string? Tenant { get; init; }
}