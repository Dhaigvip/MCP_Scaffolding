using MathApi.Mcp;
using MathApi.Models;
using MathApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MathApi.Controllers;

[ApiController]
[Route("api/math")]
[Produces("application/json")]
public sealed class MathController : ControllerBase
{
    private readonly IMathService _math;
    public MathController(IMathService math) => _math = math;

    [McpTool("Adds two numbers together and returns their sum. Use this when you need to combine values, calculate a total, or find the result of an addition. Example: A=15, B=27 returns 42.")]
    [HttpPost("add", Name = "AddNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    public IActionResult Add([FromBody] AddRequest request)
        => Ok(_math.Add(request));

    [McpTool("Subtracts B from A and returns the difference. Use this to find how much remains after removing a value or to calculate a decrease. Example: A=100, B=35 returns 65.")]
    [HttpPost("subtract", Name = "SubtractNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    public IActionResult Subtract([FromBody] SubtractRequest request)
        => Ok(_math.Subtract(request));

    [McpTool("Multiplies two numbers and returns their product. Use this to scale a value, calculate area, or apply a multiplier. Example: A=6, B=7 returns 42.")]
    [HttpPost("multiply", Name = "MultiplyNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    public IActionResult Multiply([FromBody] MultiplyRequest request)
        => Ok(_math.Multiply(request));

    [McpTool("Divides A by B and returns the quotient. Use this to split into equal parts, calculate a ratio, or find an average. Returns error code division_by_zero if B is zero. Example: A=100, B=4 returns 25.")]
    [HttpPost("divide", Name = "DivideNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MathError), StatusCodes.Status400BadRequest)]
    public IActionResult Divide([FromBody] DivideRequest request)
    {
        var (result, error) = _math.Divide(request);
        return error is not null ? BadRequest(error) : Ok(result);
    }

    [McpTool("Raises a base number to the power of an exponent. Use this for exponential growth, squares, cubes, or scientific notation. Example: Base=2, Exponent=10 returns 1024.")]
    [HttpPost("power", Name = "PowerNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    public IActionResult Power([FromBody] PowerRequest request)
        => Ok(_math.Power(request));

    [McpTool("Returns the square root of a number. Use this for geometric calculations or to reverse a square operation. Value must be non-negative — returns error code negative_sqrt otherwise. Example: Value=144 returns 12.")]
    [HttpPost("sqrt", Name = "SquareRoot")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MathError), StatusCodes.Status400BadRequest)]
    public IActionResult Sqrt([FromBody] SqrtRequest request)
    {
        var (result, error) = _math.Sqrt(request);
        return error is not null ? BadRequest(error) : Ok(result);
    }

    [McpTool("Returns the remainder after dividing A by B. Use this to check divisibility, determine odd/even, or implement cyclic logic. Returns error code modulo_by_zero if B is zero. Example: A=17, B=5 returns 2.")]
    [HttpPost("modulo", Name = "ModuloNumbers")]
    [ProducesResponseType(typeof(MathResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MathError), StatusCodes.Status400BadRequest)]
    public IActionResult Modulo([FromBody] ModuloRequest request)
    {
        var (result, error) = _math.Modulo(request);
        return error is not null ? BadRequest(error) : Ok(result);
    }
}