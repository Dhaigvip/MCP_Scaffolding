using Mcp.Agent.ModelAbstraction;
using Mcp.Swagger;

namespace Mcp.Agent.ToolSource;

/// <summary>
/// Provides tools for the agent loop.
///
/// Swagger mode: reads from <see cref="DynamicToolRegistry"/> (loaded once at startup).
/// Palma mode:   queries <see cref="Mcp.Palma.Contracts.IPalmaEndpointSource"/> per
///               session so the tool list is always version-aware.
/// </summary>
public interface IAgentToolSource
{
    /// <summary>Returns the tool list to send to the LLM for this session.</summary>
    List<AgentTool> GetTools(AgentSession session);

    /// <summary>
    /// Returns the descriptor for a named tool, or <c>null</c> if not found.
    /// Used by the agent loop to resolve the call target at invocation time.
    /// </summary>
    SwaggerToolDescriptor? GetDescriptor(string toolName, AgentSession session);
}
