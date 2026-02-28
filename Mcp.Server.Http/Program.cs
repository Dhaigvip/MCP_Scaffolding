using Mcp.Agent;
using Mcp.Governance;
using Mcp.Governance.Execution;
using Mcp.Http;
using Mcp.Palma.Contracts;
using Mcp.Swagger;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddSwaggerMcp(builder.Configuration)
    .AddGovernance(builder.Configuration)
    .AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>()
    .AddMcpServerHandlers()
    .AddAnthropic(builder.Configuration)
    .AddOpenAi(builder.Configuration)
    .AddGemini(builder.Configuration)
    .AddSwaggerAgentToolSource()
    .AddAgent()
    .AddAgentCors()
    .AddControllers();

var app = builder.Build();

app.UseAgentCors();

app.UseWebSockets();   // Must come before MapControllers so WS upgrade requests are handled

app.MapMcp("/api/mcp");
app.MapControllers();

app.Run();
