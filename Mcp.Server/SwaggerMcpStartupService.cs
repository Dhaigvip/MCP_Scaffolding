using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

/// <summary>
/// Runs the initial tool load on startup before the HTTP pipeline opens.
/// Subsequent reloads are triggered on-demand via McpAdminController.
/// </summary>
public sealed class SwaggerMcpStartupService : IHostedService
{
    private readonly ToolRefreshService _refresh;
    private readonly ILogger<SwaggerMcpStartupService> _logger;

    public SwaggerMcpStartupService(
        ToolRefreshService refresh,
        ILogger<SwaggerMcpStartupService> logger)
    {
        _refresh = refresh;
        _logger  = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _refresh.RefreshAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tools on startup. Server will start with no tools.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}