using Mcp.Governance.Execution;
using Mcp.Tooling.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public sealed class HelloTools
{
    private readonly GovernanceExecutor _exec;

    public HelloTools(GovernanceExecutor exec)
    {
        _exec = exec;
    }

    [McpServerTool, Description("Returns a friendly greeting.")]
    public Task<string> SayHello(
    string name,
    CancellationToken ct)
    {
        var args = new HelloArgs(name);

        return _exec.RunAsync<HelloArgs, string>(
        new HelloArgs(name),
        ct);
    }
}