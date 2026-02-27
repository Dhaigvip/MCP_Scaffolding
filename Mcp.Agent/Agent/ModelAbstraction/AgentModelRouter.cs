using Mcp.Agent.ModelAbstraction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mcp.Agent.Routing;

public sealed class AgentModelRouter : IAgentModelRouter
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _config;

    public AgentModelRouter(IServiceProvider sp, IConfiguration config)
    {
        _sp = sp;
        _config = config;
    }

    public IAgentModel Resolve(AgentSession session, AgentModelRequest request)
    {
        var provider =
            session.ModelProvider ??
            _config["Agent:DefaultProvider"] ??
            "anthropic";

        var model = _sp.GetKeyedService<IAgentModel>(provider);

        if (model is null)
        {
            var fallback = _config["Agent:DefaultProvider"] ?? "anthropic";
            model = _sp.GetRequiredKeyedService<IAgentModel>(fallback);
        }

        return model;
    }
}