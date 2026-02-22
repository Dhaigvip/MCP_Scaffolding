using Mcp.Core.Execution;
using Mcp.Core.Exposure;
using Mcp.Core.Registry;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Mcp.Transport.Http.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly GovernanceExecutor _executor;
    private readonly ToolRegistry _registry;
    private readonly ExposureService _exposure;

    public McpController(GovernanceExecutor executor, ToolRegistry registry, ExposureService exposure)
    {
        _executor = executor;
        _registry = registry;
        _exposure = exposure;
    }

    [HttpGet("tools")]
    public IActionResult ListTools()
    {
        var tools = _registry.GetAll()
            .Where(t => _exposure.IsExposed(t))
            .Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = t.GetInputSchema()
            });

        return Ok(tools);
    }

    [HttpPost("tools/{name}/execute")]
    public async Task<IActionResult> Execute(string name, [FromBody] object? input)
    {
        var context = new McpExecutionContext(
            User ?? new ClaimsPrincipal(),
            HttpContext.TraceIdentifier);

        var result = await _executor.ExecuteAsync(name, input, context);

        return Ok(result);
    }
}