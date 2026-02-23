using Mcp.Governance.Exposure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

/// <summary>
/// Hosted service that populates DynamicToolRegistry at application startup.
/// Replaces IStartupFilter which is unreliable in the .NET 8+ minimal hosting model.
/// StartAsync is awaited before the app begins serving requests.
/// </summary>
public sealed class SwaggerMcpStartupService : IHostedService
{
    private readonly SwaggerToolLoader _loader;
    private readonly DynamicToolRegistry _registry;
    private readonly IExposureService _exposure;
    private readonly SwaggerMcpOptions _options;
    private readonly ILogger<SwaggerMcpStartupService> _logger;

    public SwaggerMcpStartupService(
        SwaggerToolLoader loader,
        DynamicToolRegistry registry,
        IExposureService exposure,
        SwaggerMcpOptions options,
        ILogger<SwaggerMcpStartupService> logger)
    {
        _loader = loader;
        _registry = registry;
        _exposure = exposure;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Loading tools from Swagger at {Url}", _options.SwaggerUrl);
            var tools = await _loader.LoadAsync(_options.SwaggerUrl, ct);
            _registry.Initialize(tools, _exposure);
        }
        catch (Exception ex)
        {
            // Log but don't throw — server starts with empty tools rather than crashing
            _logger.LogError(ex, "Failed to load Swagger tools. MCP server will have no tools.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}