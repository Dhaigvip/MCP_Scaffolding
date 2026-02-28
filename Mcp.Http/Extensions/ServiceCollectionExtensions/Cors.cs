using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Mcp.Http;

public static class CorsServiceCollectionExtensions
{
    private const string AgentCorsPolicyName = "AgentPolicy";

    public static IServiceCollection AddAgentCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(AgentCorsPolicyName, policy =>
                policy.WithOrigins(
                        "http://localhost:5173",
                        "http://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

        return services;
    }

    public static IApplicationBuilder UseAgentCors(this IApplicationBuilder app)
        => app.UseCors(AgentCorsPolicyName);
}
