using Mcp.Server.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddSwaggerMcp(builder.Configuration)
    .AddGovernance(builder.Configuration)
    .AddMcpServerHandlers()
    .AddAnthropic(builder.Configuration)
    .AddOpenAi(builder.Configuration)
    .AddGemini(builder.Configuration)
    .AddAgent()
    .AddAgentCors()
    .AddControllers();

var app = builder.Build();

app.UseAgentCors();

app.UseWebSockets();   // Must come before MapControllers so WS upgrade requests are handled

app.MapMcp("/api/mcp");
app.MapControllers();

app.Run();