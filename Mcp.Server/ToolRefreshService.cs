using Mcp.Governance.Exposure;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

/// <summary>
/// Encapsulates the reload logic so it can be called from both
/// SwaggerMcpStartupService (on boot) and McpAdminController (on demand).
/// Registered as a singleton so the registry is always the same instance.
/// </summary>
public sealed class ToolRefreshService
{
    private readonly IToolLoader _loader;
    private readonly DynamicToolRegistry _registry;
    private readonly IExposureService _exposure;
    private readonly ILogger<ToolRefreshService> _logger;

    // Track when the last refresh happened
    private DateTimeOffset _lastRefreshedAt = DateTimeOffset.MinValue;

    public DateTimeOffset LastRefreshedAt => _lastRefreshedAt;

    public ToolRefreshService(
        IToolLoader loader,
        DynamicToolRegistry registry,
        IExposureService exposure,
        ILogger<ToolRefreshService> logger)
    {
        _loader = loader;
        _registry = registry;
        _exposure = exposure;
        _logger = logger;
    }

    /// <summary>
    /// Loads tools via the configured <see cref="IToolLoader"/>, filters against
    /// mcp_exposure.json, and atomically updates the registry.
    /// Returns a summary of what changed.
    /// </summary>
    public async Task<RegistryRefreshResult> RefreshAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Refreshing MCP tools...");

        var tools = await _loader.LoadAsync(ct);
        var result = _registry.Initialize(tools, _exposure);

        _lastRefreshedAt = DateTimeOffset.UtcNow;
        return result;
    }
}