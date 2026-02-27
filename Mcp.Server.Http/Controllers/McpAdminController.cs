using Mcp.Swagger;
using Microsoft.AspNetCore.Mvc;

namespace Mcp.Controllers;

/// <summary>
/// Admin endpoints for managing the MCP server at runtime.
/// These should be protected at the infrastructure level (reverse proxy, API key, internal network).
/// </summary>
[ApiController]
[Route("api/mcp-admin")]
public sealed class McpAdminController : ControllerBase
{
    private readonly ToolRefreshService  _refresh;
    private readonly DynamicToolRegistry _registry;

    public McpAdminController(ToolRefreshService refresh, DynamicToolRegistry registry)
    {
        _refresh  = refresh;
        _registry = registry;
    }

    /// <summary>
    /// Reloads tools from swagger.json and mcp_exposure.json without restarting the server.
    /// Call this after deploying new WebAPI endpoints or updating mcp_exposure.json.
    /// Returns a diff of what was added, removed, and the current active tool list.
    /// </summary>
    [HttpPost("refresh-tools")]
    [ProducesResponseType(typeof(RefreshToolsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RefreshToolsResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshTools(CancellationToken ct)
    {
        try
        {
            var result = await _refresh.RefreshAsync(ct);

            return Ok(new RefreshToolsResponse
            {
                Success       = true,
                Changed       = result.Changed,
                TotalActive   = result.TotalActive,
                Added         = result.Added,
                Removed       = result.Removed,
                ActiveTools   = _registry.All.Keys.Order().ToList(),
                RefreshedAt   = _refresh.LastRefreshedAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new RefreshToolsResponse
            {
                Success = false,
                Error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Returns the current state of the tool registry — what is active right now.
    /// Useful for checking what the MCP server can see without triggering a reload.
    /// </summary>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(ToolStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetTools()
    {
        var tools = _registry.All
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ToolSummary
            {
                Name        = kvp.Value.ToolName,
                Method      = kvp.Value.HttpMethod,
                Path        = kvp.Value.PathTemplate,
                Description = kvp.Value.Description,
                ParamCount  = kvp.Value.InputSchema.Properties.Count
            })
            .ToList();

        return Ok(new ToolStatusResponse
        {
            TotalActive   = tools.Count,
            LastRefreshed = _refresh.LastRefreshedAt,
            Tools         = tools
        });
    }
}

// ── Response models ───────────────────────────────────────────────────────────

public sealed class RefreshToolsResponse
{
    public bool Success           { get; init; }
    public bool Changed           { get; init; }
    public int TotalActive        { get; init; }
    public List<string> Added     { get; init; } = [];
    public List<string> Removed   { get; init; } = [];
    public List<string> ActiveTools { get; init; } = [];
    public DateTimeOffset RefreshedAt { get; init; }
    public string? Error          { get; init; }
}

public sealed class ToolStatusResponse
{
    public int TotalActive            { get; init; }
    public DateTimeOffset LastRefreshed { get; init; }
    public List<ToolSummary> Tools    { get; init; } = [];
}

public sealed class ToolSummary
{
    public string Name        { get; init; } = string.Empty;
    public string Method      { get; init; } = string.Empty;
    public string Path        { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int ParamCount     { get; init; }
}