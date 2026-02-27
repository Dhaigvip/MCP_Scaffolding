using System.Text.Json;

namespace Mcp.Agent.ModelAbstraction;

public interface IAgentModel
{
    Task<AgentModelResponse> GenerateAsync(
        AgentModelRequest request,
        CancellationToken ct);
}

public interface IAgentModelRouter
{
    IAgentModel Resolve(AgentSession session, AgentModelRequest request);
}
