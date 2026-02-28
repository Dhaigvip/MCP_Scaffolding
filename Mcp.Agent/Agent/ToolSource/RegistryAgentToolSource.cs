using Mcp.Agent.ModelAbstraction;
using Mcp.Swagger;
using System.Text.Json;

namespace Mcp.Agent.ToolSource;

/// <summary>
/// <see cref="IAgentToolSource"/> for the Swagger pipeline.
/// Reads from <see cref="DynamicToolRegistry"/> which is populated once at
/// startup — appropriate for single-version Swagger APIs.
/// </summary>
public sealed class RegistryAgentToolSource : IAgentToolSource
{
    private readonly DynamicToolRegistry _registry;

    public RegistryAgentToolSource(DynamicToolRegistry registry)
    {
        _registry = registry;
    }

    public List<AgentTool> GetTools(AgentSession session) =>
        _registry.All.Values.Select(d =>
        {
            var schemaJson = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = d.InputSchema.Properties.ToDictionary(
                    kv => kv.Key,
                    kv => new { type = kv.Value.Type, description = kv.Value.Description }),
                required = d.InputSchema.Required
            });

            return new AgentTool
            {
                Name = d.ToolName,
                Description = d.Description,
                InputJsonSchema = schemaJson
            };
        }).ToList();

    public SwaggerToolDescriptor? GetDescriptor(string toolName, AgentSession session)
    {
        _registry.TryGet(toolName, out var descriptor);
        return descriptor;
    }
}
