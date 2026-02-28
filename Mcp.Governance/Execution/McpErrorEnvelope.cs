using System;

namespace Mcp.Governance.Execution;

public sealed class McpErrorEnvelope
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Tool { get; init; }
    public string? CorrelationId { get; init; }
    public object? Details { get; init; }

    public static McpErrorEnvelope FromException(Exception ex, string tool, string correlationId)
    {
        if (ex is GovernanceException gx)
        {
            return new McpErrorEnvelope
            {
                Code = gx.Code,
                Message = gx.Message,
                Tool = tool,
                CorrelationId = correlationId,
                Details = gx.Details
            };
        }

        return new McpErrorEnvelope
        {
            Code = "internal_error",
            Message = "Unexpected error",
            Tool = tool,
            CorrelationId = correlationId
        };
    }
}