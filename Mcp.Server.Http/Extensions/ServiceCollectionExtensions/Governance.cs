using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;

namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
{
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
        services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

        return services;
    }
}