using System;
using System.Security.Claims;

namespace Mcp.Core.Execution;

public class McpExecutionContext
{
    public ClaimsPrincipal User { get; }
    public string CorrelationId { get; }

    public McpExecutionContext(ClaimsPrincipal user, string correlationId)
    {
        User = user;
        CorrelationId = correlationId;
    }
}
