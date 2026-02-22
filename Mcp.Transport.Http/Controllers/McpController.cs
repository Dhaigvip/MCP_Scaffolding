using Microsoft.AspNetCore.Mvc;
using Mcp.Core.Execution;
using Mcp.Core.Registry;
using System.Security.Claims;

namespace Mcp.Transport.Http.Controllers;

[ApiController]
[Route("mcp")]
public class McpController : ControllerBase
{
    private readonly ToolExecutor _executor;
    private readonly ToolRegistry _registry;

    public McpController(ToolExecutor executor, ToolRegistry registry)
    {
        _executor = executor;
        _registry = registry;
    }

    [HttpGet("tools")]
    public IActionResult ListTools()
    {
        var tools = _registry.GetAll().Select(t => new
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