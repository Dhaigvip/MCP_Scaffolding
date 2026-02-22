using Mcp.Core.Execution;

namespace Mcp.Core.Abstractions;

public interface IMcpTool
{
    string Name { get; }
    string Description { get; }
    object GetInputSchema();
    Task<object?> ExecuteAsync(object? input, McpExecutionContext context);
}