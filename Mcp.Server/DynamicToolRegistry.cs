using Mcp.Governance.Exposure;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Mcp.Swagger;

/// <summary>
/// Singleton registry of all active tools after:
///   1. Loading from Swagger
///   2. Filtering against mcp_exposure.json (only tools listed AND enabled survive)
///
/// This is the single source of truth that both the MCP tool handler
/// and the governance executor read from.
/// 
/// Thread-safe singleton registry of active MCP tools.
/// Supports hot reload — Initialize() can be called multiple times and swaps
/// the tool dictionary atomically so in-flight requests always see a consistent snapshot.
/// </summary>
public sealed class DynamicToolRegistry
{
    private readonly ILogger<DynamicToolRegistry> _logger;

    // Volatile reference — readers get a consistent snapshot without locking
    private volatile IReadOnlyDictionary<string, SwaggerToolDescriptor> _tools
        = new Dictionary<string, SwaggerToolDescriptor>();

    public DynamicToolRegistry(ILogger<DynamicToolRegistry> logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, SwaggerToolDescriptor> All => _tools;

    public bool TryGet(string toolName, out SwaggerToolDescriptor descriptor)
        => _tools.TryGetValue(toolName, out descriptor!);

    /// <summary>
    /// Filters loaded swagger tools against mcp_exposure.json and atomically
    /// replaces the active tool set. Safe to call repeatedly — diffs are logged.
    /// </summary>
    public RegistryRefreshResult Initialize(
        IEnumerable<SwaggerToolDescriptor> swaggerTools,
        IExposureService exposure)
    {
        var newTools = new Dictionary<string, SwaggerToolDescriptor>();

        foreach (var tool in swaggerTools)
        {
            if (!exposure.TryGetEnabledPolicy(tool.ToolName, out _))
            {
                _logger.LogDebug("Tool {Tool} skipped — not in exposure manifest or disabled.", tool.ToolName);
                continue;
            }
            newTools[tool.ToolName] = tool;
        }

        var current = _tools;
        var added = newTools.Keys.Except(current.Keys).ToList();
        var removed = current.Keys.Except(newTools.Keys).ToList();

        var result = new RegistryRefreshResult
        {
            TotalActive = newTools.Count,
            Added = added,
            Removed = removed,
            Changed = added.Count > 0 || removed.Count > 0
        };

        if (!result.Changed && current.Count > 0)
        {
            _logger.LogDebug("Tool registry refresh: no changes detected.");
            return result;
        }

        foreach (var name in added)
            _logger.LogInformation("[+] Tool {Tool} added: {Method} {Path}",
                name, newTools[name].HttpMethod, newTools[name].PathTemplate);

        foreach (var name in removed)
            _logger.LogInformation("[-] Tool {Tool} removed.", name);

        // Atomic pointer swap — no locks needed on the read path
        _tools = newTools;

        _logger.LogInformation("Tool registry updated: {Count} active tools (+{Added} -{Removed}).",
            newTools.Count, added.Count, removed.Count);

        return result;
    }
}

public sealed class RegistryRefreshResult
{
    public bool Changed { get; init; }
    public int TotalActive { get; init; }
    public List<string> Added { get; init; } = [];
    public List<string> Removed { get; init; } = [];
}
