using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Mcp.Server.Http;
using Mcp.Tooling.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<HelloTools>();

// Governance
builder.Services.AddSingleton<IExposureService, ExposureService>();
builder.Services.AddSingleton<IPolicyEngine, PolicyEngine>();
builder.Services.AddScoped<GovernanceExecutor>();

// Tooling
builder.Services.AddSingleton<IHelloTool, HelloTool>();
builder.Services.AddScoped<IToolHandler<HelloArgs, string>, HelloToolHandler>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp();

app.Run();