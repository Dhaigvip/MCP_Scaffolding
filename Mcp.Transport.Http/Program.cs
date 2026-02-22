using Mcp.Core.Execution;
using Mcp.Core.Registry;
using Mcp.Core.Abstractions;
using Mcp.Transport.Http.TestTools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Register tools
builder.Services.AddSingleton<IMcpTool, HelloTool>();

// Register ToolRegistry properly
builder.Services.AddSingleton<ToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    var tools = sp.GetServices<IMcpTool>();

    foreach (var tool in tools)
        registry.Register(tool);

    return registry;
});

builder.Services.AddSingleton<ToolExecutor>();

var app = builder.Build();

app.MapControllers();

app.Run();