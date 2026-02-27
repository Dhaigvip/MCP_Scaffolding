
using Mcp.Agent;
using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.Routing;

namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgent(this IServiceCollection services)
    {
        services.AddSingleton<SessionManager>();

        // Register router before AgentService
        services.AddSingleton<IAgentModelRouter, AgentModelRouter>();

        services.AddScoped<AgentService>();
        return services;
    }
}