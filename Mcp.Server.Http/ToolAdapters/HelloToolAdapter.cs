using Mcp.Governance.Execution;
using Mcp.Tooling;
using Mcp.Tooling.Tools;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Mcp.Server.Http.ToolAdapters;

// This is the single authoritative MCP adapter for the Hello tool.

[McpServerToolType]
public sealed class HelloToolAdapter
{
    private readonly GovernanceExecutor _exec;

    public HelloToolAdapter(GovernanceExecutor exec)
    {
        _exec = exec;
    }

    [McpServerTool(Name = ToolNames.HelloSay), Description("Returns a friendly greeting.")]
    public Task<string> SayHello(
        [Description("The name of the person to greet")] string name,
        CancellationToken ct)
    {
        return _exec.RunAsync<HelloArgs, string>(new HelloArgs(name), ct);
    }
}