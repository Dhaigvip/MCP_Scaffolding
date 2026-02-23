using Mcp.Governance.Execution;
using Mcp.Tooling.Tools;

namespace Mcp.Tooling.Handlers;

public sealed class HelloToolHandler : IToolHandler<HelloArgs, string>
{
    private readonly IHelloTool _tool;

    public HelloToolHandler(IHelloTool tool)
    {
        _tool = tool;
    }

    public string ToolName => ToolNames.HelloSay;

    public async Task<string> HandleAsync(
        HelloArgs args,
        McpExecutionContext context,
        CancellationToken ct)
    {
        var result = await _tool.SayHelloAsync(args.Name, ct);
        // Return as string; the adapter expects string, tool returns object
        return result?.ToString() ?? string.Empty;
    }
}