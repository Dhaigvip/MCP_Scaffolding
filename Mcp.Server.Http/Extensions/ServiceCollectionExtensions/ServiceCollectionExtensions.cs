using Mcp.Swagger;

namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
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
            var logger = sp.GetRequiredService<ILogger<SwaggerToolLoader>>();
            return new SwaggerToolLoader(factory.CreateClient("SwaggerLoader"), logger);
        });

        services.AddSingleton<SwaggerToolInvoker>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<SwaggerToolInvoker>>();
            return new SwaggerToolInvoker(factory.CreateClient("SwaggerInvoker"), logger);
        });

        services.AddSingleton<DynamicToolRegistry>();
        services.AddSingleton<ToolRefreshService>();
        services.AddScoped<DynamicMcpToolHandler>();
        services.AddHostedService<SwaggerMcpStartupService>();

        return services;
    }
}