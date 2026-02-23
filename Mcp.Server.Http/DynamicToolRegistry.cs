using Mcp.Governance.Exposure;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

/// <summary>
/// Singleton registry of all active tools after:
///   1. Loading from Swagger
///   2. Filtering against mcp_exposure.json (only tools listed AND enabled survive)
///
/// This is the single source of truth that both the MCP tool handler
/// and the governance executor read from.
/// </summary>
public sealed class DynamicToolRegistry
{
    private readonly ILogger<DynamicToolRegistry> _logger;

    // Immutable after Initialize() — safe to read from multiple threads
    private IReadOnlyDictionary<string, SwaggerToolDescriptor> _tools
        = new Dictionary<string, SwaggerToolDescriptor>();

    public DynamicToolRegistry(ILogger<DynamicToolRegistry> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<SwaggerToolDescriptor> All => _tools.Values.ToList();

    public bool TryGet(string toolName, out SwaggerToolDescriptor descriptor)
        => _tools.TryGetValue(toolName, out descriptor!);

    /// <summary>
    /// Called once at startup by SwaggerMcpStartupFilter.
    /// Merges the swagger tools with the exposure manifest.
    /// Only tools present AND enabled in mcp_exposure.json are kept.
    /// </summary>
    public void Initialize(
        IEnumerable<SwaggerToolDescriptor> swaggerTools,
        IExposureService exposure)
    {
        var dict = new Dictionary<string, SwaggerToolDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in swaggerTools)
        {
            if (!exposure.TryGetEnabledPolicy(tool.ToolName, out _))
            {
                _logger.LogDebug("Tool {Name} skipped — not in exposure manifest or disabled.", tool.ToolName);
                continue;
            }

            dict[tool.ToolName] = tool;
            _logger.LogInformation("Tool {Name} activated: {Method} {Path}",
                tool.ToolName, tool.HttpMethod, tool.PathTemplate);
        }

        _tools = dict;
        _logger.LogInformation("{Count} tools active after exposure filtering.", dict.Count);
    }
}