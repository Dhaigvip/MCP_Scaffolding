using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSwaggerMcp(
        this IServiceCollection services,
        IConfiguration config)
    {
        var swaggerMcpOptions = config
            .GetSection("SwaggerMcp")
            .Get<SwaggerMcpOptions>()
            ?? throw new InvalidOperationException("SwaggerMcp config section is required.");

        services.AddSingleton(swaggerMcpOptions);

        services.AddHttpClient("SwaggerLoader", c =>
        {
            c.BaseAddress = new Uri(swaggerMcpOptions.SwaggerUrl
                .Replace("/swagger/v1/swagger.json", "")
                .Replace("/swagger/v2/swagger.json", ""));
        });

        services.AddHttpClient("SwaggerInvoker", c =>
        {
            c.BaseAddress = new Uri(swaggerMcpOptions.ApiBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<SwaggerToolLoader>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger  = sp.GetRequiredService<ILogger<SwaggerToolLoader>>();
            return new SwaggerToolLoader(factory.CreateClient("SwaggerLoader"), logger);
        });

        services.AddSingleton<SwaggerToolInvoker>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger  = sp.GetRequiredService<ILogger<SwaggerToolInvoker>>();
            return new SwaggerToolInvoker(factory.CreateClient("SwaggerInvoker"), logger);
        });

        services.AddSingleton<DynamicToolRegistry>();
        services.AddSingleton<ToolRefreshService>();
        services.AddScoped<DynamicMcpToolHandler>();
        services.AddHostedService<SwaggerMcpStartupService>();

        return services;
    }

    public static IServiceCollection AddMcpServerHandlers(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithListToolsHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<DynamicMcpToolHandler>();
                return await handler.ListToolsAsync(ctx, ct);
            })
            .WithCallToolHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<DynamicMcpToolHandler>();
                return await handler.CallToolAsync(ctx, ct);
            });

        return services;
    }
}
