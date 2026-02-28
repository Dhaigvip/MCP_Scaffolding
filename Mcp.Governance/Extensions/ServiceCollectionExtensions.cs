using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mcp.Governance;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core governance services: options, exposure manifest, policy engine.
    /// The HTTP-specific <see cref="ICorrelationIdAccessor"/> implementation must be
    /// registered separately by the host (e.g. Mcp.Server.Http's Program.cs).
    /// </summary>
    public static IServiceCollection AddGovernance(
        this IServiceCollection services,
        IConfiguration config)
    {
        var governanceOptions = config
            .GetSection("Governance")
            .Get<GovernanceOptions>() ?? new GovernanceOptions();

        services.AddSingleton(governanceOptions);

        var manifest = new ConfigurationBuilder()
            .AddJsonFile("mcp_exposure.json", optional: false, reloadOnChange: false)
            .Build()
            .Get<ExposureManifest>()
            ?? throw new InvalidOperationException("mcp_exposure.json is missing or invalid.");

        services.AddSingleton(manifest);
        services.AddSingleton<IExposureService, ExposureService>();
        services.AddSingleton<IPolicyEngine, PolicyEngine>();

        return services;
    }
}
