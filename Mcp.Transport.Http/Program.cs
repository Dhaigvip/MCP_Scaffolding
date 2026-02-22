using Mcp.Core.Abstractions;
using Mcp.Core.Execution;
using Mcp.Core.Exposure;
using Mcp.Core.Governance;
using Mcp.Core.Registry;
using Mcp.Transport.Http.TestTools;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

//builder.Services.AddSingleton(new ExposureManifest
//{
//    Tools = new List<ToolExposure>
//    {
//        new ToolExposure
//        {
//            ToolName = "system.hello",
//            Enabled = true
//        }
//    }
//});

builder.Services.Configure<ExposureManifest>(
    builder.Configuration);

builder.Services.AddSingleton<ExposureService>(sp =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<ExposureManifest>>();
    return new ExposureService(options);
});


builder.Services.AddSingleton<ExposureService>();
builder.Services.AddSingleton<PolicyEngine>();
builder.Services.AddSingleton<GovernanceExecutor>();

// Register tools
builder.Services.AddSingleton<IMcpTool, HelloTool>();

builder.Services.AddSingleton(new GovernanceOptions
{
    MaximumAllowedRisk = RiskLevel.Read
});

// Register ToolRegistry properly
builder.Services.AddSingleton<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    var tools = sp.GetServices<IMcpTool>();

    foreach (var tool in tools)
        registry.Register(tool);

    return registry;
});

builder.Services.AddSingleton<GovernanceExecutor>();

builder.Configuration
    .AddJsonFile("mcp.exposure.json", optional: false, reloadOnChange: true);

var app = builder.Build();

app.MapControllers();

app.Run();