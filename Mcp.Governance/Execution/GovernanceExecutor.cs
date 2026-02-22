using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Mcp.Governance.Execution;

public sealed class GovernanceExecutor
{
    private readonly IExposureService _exposure;
    private readonly IPolicyEngine _policy;
    private readonly IServiceProvider _services;
    private readonly GovernanceOptions _options;
    private readonly Func<ClaimsPrincipal> _principalAccessor;
    private readonly Func<string> _correlationIdAccessor;

    public GovernanceExecutor(
        IExposureService exposure,
        IPolicyEngine policy,
        IServiceProvider services,
        GovernanceOptions options,
        Func<ClaimsPrincipal> principalAccessor,
        Func<string> correlationIdAccessor)
    {
        _exposure = exposure;
        _policy = policy;
        _services = services;
        _options = options;
        _principalAccessor = principalAccessor;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<TResult> RunAsync<TArgs, TResult>(
        TArgs args,
        CancellationToken ct)
    {
        var handler = _services.GetRequiredService<IToolHandler<TArgs, TResult>>();
        var toolName = handler.ToolName;

        var correlationId = _correlationIdAccessor();
        var principal = _principalAccessor();

        if (!_exposure.IsEnabled(toolName))
            throw GovernanceException.NotFound(toolName);

        if (!_exposure.TryGetToolPolicy(toolName, out var policy))
            throw GovernanceException.NotFound(toolName);

        var tenant = _options.TenantResolver();
        var globalMaxRisk = _exposure.Current.GlobalMaxRisk;

        _policy.EnforceToolPolicy(toolName, policy, principal, globalMaxRisk, tenant);

        var ctx = new McpExecutionContext
        {
            ToolName = toolName,
            Principal = principal,
            CorrelationId = correlationId,
            Tenant = tenant
        };

        return await handler.HandleAsync(args, ctx, ct);
    }
}