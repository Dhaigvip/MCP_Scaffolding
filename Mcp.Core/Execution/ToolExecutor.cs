namespace Mcp.Core.Execution;

using Mcp.Core.Registry;

public class ToolExecutor
{
    private readonly ToolRegistry _registry;

    public ToolExecutor(ToolRegistry registry)
    {
        _registry = registry;
    }

    public async Task<object> ExecuteAsync(
        string toolName,
        object? input,
        McpExecutionContext context)
    {
        var tool = _registry.Get(toolName);
        if (tool == null)
        {
            return new McpErrorEnvelope("TOOL_NOT_FOUND", $"Tool '{toolName}' not registered.");
        }

        try
        {
            var result = await tool.ExecuteAsync(input, context);
            return new { ok = true, result };
        }
        catch (Exception ex)
        {
            return new McpErrorEnvelope("EXECUTION_ERROR", ex.Message);
        }
    }
}