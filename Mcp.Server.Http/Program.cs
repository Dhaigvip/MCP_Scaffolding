using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Mcp.Server.Http.ToolAdapters;
using Mcp.Tooling.Handlers;
using Mcp.Tooling.Tools;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure ─────────────────────────────────────────────────────────
// No AddAuthentication / AddAuthorization — handled by the host WebAPI.
// IHttpContextAccessor is still needed for correlation ID resolution.
builder.Services.AddHttpContextAccessor();

// ── MCP Server ─────────────────────────────────────────────────────────────
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<HelloToolAdapter>();

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

// ── Governance: Policy & Execution ─────────────────────────────────────────
builder.Services.AddSingleton<IPolicyEngine, PolicyEngine>();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
builder.Services.AddScoped<GovernanceExecutor>();

// ── Tooling ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IHelloTool, HelloTool>();
builder.Services.AddScoped<IToolHandler<HelloArgs, string>, HelloToolHandler>();

// ── Pipeline ───────────────────────────────────────────────────────────────
var app = builder.Build();

// No UseAuthentication / UseAuthorization — host WebAPI owns that pipeline.

app.MapMcp();
app.MapMcp("/api/mcp");

app.Run();