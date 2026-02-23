using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Mcp.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure ─────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ── Swagger MCP Options ────────────────────────────────────────────────────
var swaggerMcpOptions = builder.Configuration
    .GetSection("SwaggerMcp")
    .Get<SwaggerMcpOptions>()
    ?? throw new InvalidOperationException("SwaggerMcp config section is required.");

builder.Services.AddSingleton(swaggerMcpOptions);

// ── HttpClient for Swagger loader and tool invoker ─────────────────────────
// Two named clients: one for fetching swagger.json, one for invoking APIs.
builder.Services.AddHttpClient<SwaggerToolLoader>(c =>
{
    // Swagger loader only needs to reach the swagger.json endpoint
    c.BaseAddress = new Uri(swaggerMcpOptions.SwaggerUrl
        .Replace("/swagger/v1/swagger.json", "")
        .Replace("/swagger/v2/swagger.json", ""));
});

builder.Services.AddHttpClient<SwaggerToolInvoker>(c =>
{
    c.BaseAddress = new Uri(swaggerMcpOptions.ApiBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(30);
});

// ── Swagger Tool Pipeline ──────────────────────────────────────────────────
builder.Services.AddSingleton<DynamicToolRegistry>();
builder.Services.AddScoped<DynamicMcpToolHandler>();
builder.Services.AddHostedService<SwaggerMcpStartupService>();

// ── Governance: Options ────────────────────────────────────────────────────
var governanceOptions = builder.Configuration
    .GetSection("Governance")
    .Get<GovernanceOptions>() ?? new GovernanceOptions();

builder.Services.AddSingleton(governanceOptions);

// ── Governance: Exposure Manifest ──────────────────────────────────────────
var manifest = builder.Configuration
    .AddJsonFile("mcp_exposure.json", optional: false, reloadOnChange: false)
    .Build()
    .Get<ExposureManifest>()
    ?? throw new InvalidOperationException("mcp_exposure.json is missing or invalid.");

builder.Services.AddSingleton(manifest);
builder.Services.AddSingleton<IExposureService, ExposureService>();

// ── Governance: Policy ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IPolicyEngine, PolicyEngine>();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

// ── MCP Server ─────────────────────────────────────────────────────────────
// No .WithTools<T>() — tools are served dynamically via custom handlers.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithListToolsHandler(async (ctx, ct) =>
    {
        var handler = ctx.Server.Services
            .CreateScope().ServiceProvider          // ← create a scope to resolve Scoped services
            .GetRequiredService<DynamicMcpToolHandler>();
        return await handler.ListToolsAsync(ctx, ct);
    })
    .WithCallToolHandler(async (ctx, ct) =>
    {
        var handler = ctx.Server.Services
            .CreateScope().ServiceProvider
            .GetRequiredService<DynamicMcpToolHandler>();
        return await handler.CallToolAsync(ctx, ct);
    });

// ── Pipeline ───────────────────────────────────────────────────────────────
var app = builder.Build();

app.MapMcp("/api/mcp");

app.Run();