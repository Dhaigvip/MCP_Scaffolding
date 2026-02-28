using Mcp.Palma;
using Mcp.Palma.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

public static class ServiceCollectionExtensions
{
    // ── Swagger pipeline ──────────────────────────────────────────────────────

    /// <summary>
    /// Registers the Swagger → MCP pipeline: fetches swagger.json on startup and
    /// exposes operations as MCP tools via a static DynamicToolRegistry.
    /// Pair with <see cref="AddMcpServerHandlers"/> (Swagger handler).
    /// </summary>
    public static IServiceCollection AddSwaggerMcp(
        this IServiceCollection services,
        IConfiguration config)
    {
        var options = config
            .GetSection("SwaggerMcp")
            .Get<SwaggerMcpOptions>()
            ?? throw new InvalidOperationException("SwaggerMcp config section is required.");

        services.AddSingleton(options);

        services.AddHttpClient("SwaggerLoader", c =>
        {
            c.BaseAddress = new Uri(options.SwaggerUrl
                .Replace("/swagger/v1/swagger.json", "")
                .Replace("/swagger/v2/swagger.json", ""));
        });

        services.AddHttpClient("SwaggerInvoker", c =>
        {
            c.BaseAddress = new Uri(options.ApiBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IToolLoader>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<SwaggerToolLoader>>();
            return new SwaggerToolLoader(factory.CreateClient("SwaggerLoader"), options, logger);
        });

        services.AddSingleton<IToolInvoker>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<SwaggerToolInvoker>>();
            return new SwaggerToolInvoker(factory.CreateClient("SwaggerInvoker"), logger);
        });

        return services.AddMcpCore();
    }

    // ── Palma in-process pipeline ─────────────────────────────────────────────

    /// <summary>
    /// Registers the Palma → MCP in-process pipeline.
    ///
    /// Tools are NOT preloaded at startup.  Instead, <see cref="PalmaMcpToolHandler"/>
    /// builds the tool list on every <c>tools/list</c> request using the session's
    /// version — so each session sees exactly its API version's tools.
    ///
    /// Prerequisites — register before calling this:
    ///   <list type="bullet">
    ///     <item><see cref="IPalmaEndpointSource"/> — endpoint discovery (in-process).</item>
    ///     <item><see cref="IPalmaToolInvoker"/>    — tool execution (in-process, no HTTP).</item>
    ///   </list>
    ///
    /// Pair with <see cref="AddPalmaMcpServerHandlers"/> (Palma handler).
    /// </summary>
    public static IServiceCollection AddPalmaMcp(
        this IServiceCollection services,
        IConfiguration config)
    {
        var options = config
            .GetSection("PalmaMcp")
            .Get<PalmaMcpOptions>()
            ?? throw new InvalidOperationException("PalmaMcp config section is required.");

        services.AddSingleton(options);

        // PalmaToolInvoker adapts IToolInvoker → IPalmaToolInvoker (used by AgentService)
        services.AddSingleton<IToolInvoker>(sp =>
        {
            var inProcess = sp.GetRequiredService<IPalmaToolInvoker>();
            return new PalmaToolInvoker(inProcess);
        });

        // PalmaMcpToolHandler handles the MCP protocol (tools/list + tools/call) per session
        services.AddScoped<PalmaMcpToolHandler>();

        // No IToolLoader, no DynamicToolRegistry, no SwaggerMcpStartupService —
        // Palma tools are resolved dynamically per session in PalmaMcpToolHandler.

        return services;
    }

    // ── Palma remote (separate-process) pipeline ──────────────────────────────

    /// <summary>
    /// Registers the Palma → MCP remote pipeline for the separate-process scenario.
    ///
    /// Authentication uses the OAuth2 client credentials flow:
    ///   1. MCP server obtains a bearer token from the configured identity provider.
    ///   2. Every request to the Palma API carries  Authorization: Bearer {token}
    ///      and  AuthScheme: {SchemeId}.
    ///
    /// Tool discovery calls: GET {ApiBaseUrl}/api/mcp/endpoints  (configurable via EndpointsPath).
    /// The Palma web API must expose this endpoint — see McpEndpointsController.
    ///
    /// Pair with <see cref="AddMcpServerHandlers"/>.
    /// </summary>
    public static IServiceCollection AddPalmaMcpRemote(
        this IServiceCollection services,
        IConfiguration config)
    {
        var options = config
            .GetSection("PalmaMcpRemote")
            .Get<PalmaRemoteMcpOptions>()
            ?? throw new InvalidOperationException("PalmaMcpRemote config section is required.");

        services.AddSingleton(options);

        services.AddHttpClient("PalmaRemote", c =>
        {
            c.BaseAddress = new Uri(options.ApiBaseUrl);
            c.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<PalmaTokenProvider>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<PalmaTokenProvider>>();
            return new PalmaTokenProvider(factory.CreateClient("PalmaRemote"), options, logger);
        });

        services.AddSingleton<IToolLoader>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var tokens = sp.GetRequiredService<PalmaTokenProvider>();
            var logger = sp.GetRequiredService<ILogger<PalmaRemoteToolLoader>>();
            return new PalmaRemoteToolLoader(factory.CreateClient("PalmaRemote"), options, tokens, logger);
        });

        services.AddSingleton<IToolInvoker>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var tokens = sp.GetRequiredService<PalmaTokenProvider>();
            var logger = sp.GetRequiredService<ILogger<PalmaRemoteToolInvoker>>();
            return new PalmaRemoteToolInvoker(factory.CreateClient("PalmaRemote"), options, tokens, logger);
        });

        return services.AddMcpCore();
    }

    // ── MCP server handlers ───────────────────────────────────────────────────

    /// <summary>
    /// Wires <see cref="DynamicMcpToolHandler"/> (Swagger/remote — static registry) into the
    /// MCP SDK.  Use this with <see cref="AddSwaggerMcp"/> or <see cref="AddPalmaMcpRemote"/>.
    /// </summary>
    public static IServiceCollection AddMcpServerHandlers(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithListToolsHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services!.CreateScope();
                return await scope.ServiceProvider
                    .GetRequiredService<DynamicMcpToolHandler>()
                    .ListToolsAsync(ctx, ct);
            })
            .WithCallToolHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services!.CreateScope();
                return await scope.ServiceProvider
                    .GetRequiredService<DynamicMcpToolHandler>()
                    .CallToolAsync(ctx, ct);
            });

        return services;
    }

    /// <summary>
    /// Wires <see cref="PalmaMcpToolHandler"/> (Palma in-process — dynamic, version-aware)
    /// into the MCP SDK.  Use this with <see cref="AddPalmaMcp"/>.
    /// </summary>
    public static IServiceCollection AddPalmaMcpServerHandlers(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithListToolsHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services!.CreateScope();
                return await scope.ServiceProvider
                    .GetRequiredService<PalmaMcpToolHandler>()
                    .ListToolsAsync(ctx, ct);
            })
            .WithCallToolHandler(async (ctx, ct) =>
            {
                using var scope = ctx.Server.Services!.CreateScope();
                return await scope.ServiceProvider
                    .GetRequiredService<PalmaMcpToolHandler>()
                    .CallToolAsync(ctx, ct);
            });

        return services;
    }

    // ── Shared (Swagger / Remote) ─────────────────────────────────────────────

    private static IServiceCollection AddMcpCore(this IServiceCollection services)
    {
        services.AddSingleton<DynamicToolRegistry>();
        services.AddSingleton<ToolRefreshService>();
        services.AddScoped<DynamicMcpToolHandler>();
        services.AddHostedService<SwaggerMcpStartupService>();
        return services;
    }
}
