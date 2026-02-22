using Mcp.Governance.Execution;
using Mcp.Tooling.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mcp.Server.Http.ToolAdapters;

[McpServerToolType]
public sealed class HelloToolAdapter
{
    private readonly GovernanceExecutor _exec;

    public HelloToolAdapter(GovernanceExecutor exec)
    {
        _exec = exec;
    }

    [McpServerTool, Description("Returns a friendly greeting.")]
    public Task<string> SayHello(
    [Description("Name to greet")] string name,
    CancellationToken ct)
    {
        return _exec.RunAsync<HelloArgs, string>(
            new HelloArgs(name),
            ct);
    }
}