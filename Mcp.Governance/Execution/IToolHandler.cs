namespace Mcp.Governance.Execution;

public interface IToolHandler<TArgs, TResult>
{
    string ToolName { get; }

    Task<TResult> HandleAsync(
        TArgs args,
        McpExecutionContext context,
        CancellationToken ct);
}