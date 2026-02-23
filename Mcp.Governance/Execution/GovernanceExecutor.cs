using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Microsoft.Extensions.DependencyInjection;

namespace Mcp.Governance.Execution;

/// <summary>
/// Execution pipeline without authentication/authorization.
/// Auth is handled by the host WebAPI that embeds this MCP server.
/// Responsibilities here: exposure gating, risk gate, tenant check, correlation tracking.
/// </summary>
public sealed class GovernanceExecutor
{
    private readonly IExposureService _exposure;
    private readonly IPolicyEngine _policy;
    private readonly IServiceProvider _services;
    private readonly GovernanceOptions _options;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public GovernanceExecutor(
        IExposureService exposure,
        IPolicyEngine policy,
        IServiceProvider services,
        GovernanceOptions options,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _exposure = exposure;
        _policy = policy;
        _services = services;
        _options = options;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<TResult> RunAsync<TArgs, TResult>(
        TArgs args,
        CancellationToken ct)
    {
        var handler = _services.GetRequiredService<IToolHandler<TArgs, TResult>>();
        var toolName = handler.ToolName;

        if (!_exposure.TryGetEnabledPolicy(toolName, out var policy))
            throw GovernanceException.NotFound(toolName);

        var tenant = _options.TenantResolver();
        var globalMaxRisk = _exposure.Current.GlobalMaxRisk;

        _policy.EnforceToolPolicy(toolName, policy, globalMaxRisk, tenant);

        var ctx = new McpExecutionContext
        {
            ToolName = toolName,
            CorrelationId = _correlationIdAccessor.CorrelationId,
            Tenant = tenant
        };

        return await handler.HandleAsync(args, ctx, ct);
    }
}