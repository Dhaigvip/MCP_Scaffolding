using Mcp.Core.Abstractions;
using Mcp.Core.Execution;

namespace Mcp.Transport.Http.TestTools;

public class HelloTool : IMcpTool
{
    public string Name => "system.hello";
    public string Description => "Returns a greeting message.";

    public object GetInputSchema() => new
    {
        type = "object",
        properties = new
        {
            name = new { type = "string" }
        }
    };

    public Task<object?> ExecuteAsync(object? input, McpExecutionContext context)
    {
        return Task.FromResult<object?>(
            new { message = "Hello from MCP server." });
    }
}