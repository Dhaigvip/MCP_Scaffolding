using Mcp.Swagger;

namespace Mcp.Server.Http.Extensions;

public static partial class ServiceCollectionExtensions
{
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