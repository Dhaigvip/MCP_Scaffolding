using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MathApi.Models;

public sealed class AddRequest
{
    [Description("The first number to add. Accepts any positive or negative decimal value.")]
    [Required] public double A { get; init; }

    [Description("The second number to add. Accepts any positive or negative decimal value.")]
    [Required] public double B { get; init; }
}

public sealed class SubtractRequest
{
    [Description("The number to subtract from (minuend). Accepts any positive or negative decimal value.")]
    [Required] public double A { get; init; }

    [Description("The number to subtract (subtrahend). Accepts any positive or negative decimal value.")]
    [Required] public double B { get; init; }
}

public sealed class MultiplyRequest
{
    [Description("The first factor to multiply. Accepts any positive or negative decimal value.")]
    [Required] public double A { get; init; }

    [Description("The second factor to multiply. Accepts any positive or negative decimal value.")]
    [Required] public double B { get; init; }
}

public sealed class DivideRequest
{
    [Description("The dividend — the number to be divided. Accepts any positive or negative decimal value.")]
    [Required] public double A { get; init; }

    [Description("The divisor — must not be zero or a division_by_zero error is returned.")]
    [Required] public double B { get; init; }
}

public sealed class PowerRequest
{
    [Description("The base number to raise. For example 2 in '2 to the power of 8'. Accepts any decimal value.")]
    [Required] public double Base { get; init; }

    [Description("The exponent — how many times to multiply base by itself. Use 2 for square, 3 for cube. Accepts decimals and negatives.")]
    [Required] public double Exponent { get; init; }
}

public sealed class SqrtRequest
{
    [Description("The number to find the square root of. Must be zero or positive — negatives return a negative_sqrt error. Example: 144 returns 12.")]
    [Required]
    [Range(0, double.MaxValue)]
    public double Value { get; init; }
}

public sealed class ModuloRequest
{
    [Description("The dividend — the number to divide. Accepts any positive or negative decimal value.")]
    [Required] public double A { get; init; }

    [Description("The divisor — must not be zero or a modulo_by_zero error is returned.")]
    [Required] public double B { get; init; }
}

public sealed class MathResult
{
    public double Result     { get; init; }
    public string Expression { get; init; } = string.Empty;
}

public sealed class MathError
{
    public string Code    { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}