using Mcp.Palma.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mcp.Http.Controllers;

/// <summary>
/// Exposes the Palma endpoint list over HTTP so that an external MCP agent
/// (Scenario 3 — separate process) can discover available tools via
/// <c>GET /api/mcp/endpoints</c>.
///
/// Used by <see cref="Mcp.Swagger.PalmaRemoteToolLoader"/> which calls this
/// path on startup to build the tool registry without needing a Swagger spec.
///
/// In-process scenarios (Scenario 2) use <see cref="IPalmaEndpointSource"/>
/// directly and never call this endpoint.
/// </summary>
[ApiController]
[Route("api/mcp")]
public sealed class McpEndpointsController : ControllerBase
{
    private readonly IPalmaEndpointSource _source;

    public McpEndpointsController(IPalmaEndpointSource source)
    {
        _source = source;
    }

    /// <summary>
    /// Returns the list of Palma endpoints available as MCP tools for the given API version.
    /// Called by <c>PalmaRemoteToolLoader</c> on startup to discover tools.
    /// </summary>
    /// <param name="version">API version, e.g. "1". Required.</param>
    [HttpGet("endpoints")]
    [ProducesResponseType(typeof(List<PalmaEndpointInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GetEndpoints([FromQuery] string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return BadRequest("version query parameter is required.");

        return Ok(_source.GetEndpoints(version).ToList());
    }
}
