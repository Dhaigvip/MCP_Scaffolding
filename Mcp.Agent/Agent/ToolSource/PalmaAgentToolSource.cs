using Mcp.Agent.ModelAbstraction;
using Mcp.Governance.Exposure;
using Mcp.Palma;
using Mcp.Palma.Contracts;
using Mcp.Swagger;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Mcp.Agent.ToolSource;

/// <summary>
/// <see cref="IAgentToolSource"/> for the Palma in-process pipeline.
///
/// Builds the tool list fresh on every call by querying
/// <see cref="IPalmaEndpointSource.GetEndpoints(string)"/> with the session's
/// version — so each session sees exactly the tools available in its API version.
/// No static registry is involved.
/// </summary>
public sealed class PalmaAgentToolSource : IAgentToolSource
{
    private readonly IPalmaEndpointSource _source;
    private readonly PalmaMcpOptions _options;
    private readonly IExposureService _exposure;
    private readonly ILogger<PalmaAgentToolSource> _logger;

    public PalmaAgentToolSource(
        IPalmaEndpointSource source,
        PalmaMcpOptions options,
        IExposureService exposure,
        ILogger<PalmaAgentToolSource> logger)
    {
        _source = source;
        _options = options;
        _exposure = exposure;
        _logger = logger;
    }

    public List<AgentTool> GetTools(AgentSession session)
    {
        var descriptors = BuildDescriptors(session);

        return descriptors.Select(d =>
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
    }

    public SwaggerToolDescriptor? GetDescriptor(string toolName, AgentSession session)
    {
        var descriptors = BuildDescriptors(session);
        return descriptors.FirstOrDefault(d => d.ToolName == toolName);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private List<SwaggerToolDescriptor> BuildDescriptors(AgentSession session)
    {
        var version = session.PalmaContext?.Version
            ?? throw new InvalidOperationException(
                "Session has no PalmaContext. Ensure PalmaContext is supplied at session creation.");

        var contextParams = new HashSet<string>(
            _options.ContextQueryParams,
            StringComparer.OrdinalIgnoreCase);

        var endpoints = _source.GetEndpoints(version);

        // Build descriptors for all exposed+enabled endpoints
        var all = PalmaToolBuilder.Build(endpoints, contextParams, _logger);

        return all
            .Where(d => _exposure.TryGetEnabledPolicy(d.ToolName, out _))
            .ToList();
    }
}
