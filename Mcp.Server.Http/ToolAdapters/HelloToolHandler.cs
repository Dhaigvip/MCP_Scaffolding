using Mcp.Governance.Execution;
using Mcp.Tooling.Tools;

public sealed class HelloToolHandler
    : IToolHandler<HelloArgs, string>
{
    public string ToolName => "hello.say";

    public Task<string> HandleAsync(
        HelloArgs args,
        McpExecutionContext context,
        CancellationToken ct)
    {
        return Task.FromResult($"Hello, {args.Name}");
    }
}