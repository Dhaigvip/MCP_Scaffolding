using System.ComponentModel.DataAnnotations;

namespace MathApi.Models;

// ── Add ───────────────────────────────────────────────────────────────────────

/// <summary>Two numbers to be added together. Provide both A and B as numeric values.</summary>
public sealed class AddRequest
{
    /// <summary>The first number (left operand). Accepts any positive or negative decimal value.</summary>
    [Required]
    public double A { get; init; }

    /// <summary>The second number (right operand). Accepts any positive or negative decimal value.</summary>
    [Required]
    public double B { get; init; }
}

// ── Subtract ──────────────────────────────────────────────────────────────────

/// <summary>Two numbers for subtraction. The result is A minus B.</summary>
public sealed class SubtractRequest
{
    /// <summary>The number to subtract from (minuend). Accepts any positive or negative decimal value.</summary>
    [Required]
    public double A { get; init; }

    /// <summary>The number to subtract (subtrahend). Accepts any positive or negative decimal value.</summary>
    [Required]
    public double B { get; init; }
}

// ── Multiply ──────────────────────────────────────────────────────────────────

/// <summary>Two numbers to be multiplied together. The result is A times B.</summary>
public sealed class MultiplyRequest
{
    /// <summary>The first factor. Accepts any positive or negative decimal value.</summary>
    [Required]
    public double A { get; init; }

    /// <summary>The second factor. Accepts any positive or negative decimal value.</summary>
    [Required]
    public double B { get; init; }
}

// ── Divide ────────────────────────────────────────────────────────────────────

/// <summary>Two numbers for division. The result is A divided by B. B must not be zero.</summary>
public sealed class DivideRequest
{
    /// <summary>The dividend — the number to be divided. Accepts any positive or negative decimal value.</summary>
    [Required]
    public double A { get; init; }

    /// <summary>The divisor — the number to divide by. Must not be zero or a division_by_zero error is returned.</summary>
    [Required]
    public double B { get; init; }
}

// ── Power ─────────────────────────────────────────────────────────────────────

/// <summary>A base and exponent for a power operation. The result is Base raised to the power of Exponent.</summary>
public sealed class PowerRequest
{
    /// <summary>The base number to raise. For example 2 in "2 to the power of 8". Accepts any decimal value.</summary>
    [Required]
    public double Base { get; init; }

    /// <summary>The exponent — how many times to multiply the base by itself. Use 2 for square, 3 for cube. Accepts any decimal value including fractions and negatives.</summary>
    [Required]
    public double Exponent { get; init; }
}

// ── Square Root ───────────────────────────────────────────────────────────────

/// <summary>A single non-negative number to find the square root of.</summary>
public sealed class SqrtRequest
{
    /// <summary>The number to find the square root of. Must be zero or positive — negative values return a negative_sqrt error. Example: 144 returns 12.</summary>
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Value must be non-negative.")]
    public double Value { get; init; }
}

// ── Modulo ────────────────────────────────────────────────────────────────────

/// <summary>Two numbers for a modulo operation. Returns the remainder after dividing A by B.</summary>
public sealed class ModuloRequest
{
    /// <summary>The dividend — the number to be divided. Accepts any positive or negative decimal value.</summary>
    [Required]
    public double A { get; init; }

    /// <summary>The divisor — must not be zero or a modulo_by_zero error is returned. Accepts any non-zero decimal value.</summary>
    [Required]
    public double B { get; init; }
}

// ── Shared response ───────────────────────────────────────────────────────────

/// <summary>The result of a successful math operation.</summary>
public sealed class MathResult
{
    /// <summary>The numeric result of the calculation.</summary>
    public double Result { get; init; }

    /// <summary>A human-readable expression showing the full calculation e.g. "15 + 27 = 42".</summary>
    public string Expression { get; init; } = string.Empty;
}

/// <summary>Returned when a math operation cannot be completed due to invalid input.</summary>
public sealed class MathError
{
    /// <summary>Machine-readable error code. Possible values: division_by_zero, negative_sqrt, modulo_by_zero.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable explanation of what went wrong and how to fix the input.</summary>
    public string Message { get; init; } = string.Empty;
}