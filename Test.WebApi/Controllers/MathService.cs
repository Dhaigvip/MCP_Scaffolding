using MathApi.Models;

namespace MathApi.Services;

public interface IMathService
{
    MathResult Add(AddRequest req);
    MathResult Subtract(SubtractRequest req);
    MathResult Multiply(MultiplyRequest req);
    (MathResult? Result, MathError? Error) Divide(DivideRequest req);
    MathResult Power(PowerRequest req);
    (MathResult? Result, MathError? Error) Sqrt(SqrtRequest req);
    (MathResult? Result, MathError? Error) Modulo(ModuloRequest req);
}

public sealed class MathService : IMathService
{
    public MathResult Add(AddRequest req) => new()
    {
        Result     = req.A + req.B,
        Expression = $"{req.A} + {req.B} = {req.A + req.B}"
    };

    public MathResult Subtract(SubtractRequest req) => new()
    {
        Result     = req.A - req.B,
        Expression = $"{req.A} - {req.B} = {req.A - req.B}"
    };

    public MathResult Multiply(MultiplyRequest req) => new()
    {
        Result     = req.A * req.B,
        Expression = $"{req.A} × {req.B} = {req.A * req.B}"
    };

    public (MathResult? Result, MathError? Error) Divide(DivideRequest req)
    {
        if (req.B == 0)
            return (null, new MathError { Code = "division_by_zero", Message = "Divisor (B) cannot be zero." });

        var result = req.A / req.B;
        return (new MathResult { Result = result, Expression = $"{req.A} ÷ {req.B} = {result}" }, null);
    }

    public MathResult Power(PowerRequest req)
    {
        var result = Math.Pow(req.Base, req.Exponent);
        return new MathResult { Result = result, Expression = $"{req.Base}^{req.Exponent} = {result}" };
    }

    public (MathResult? Result, MathError? Error) Sqrt(SqrtRequest req)
    {
        if (req.Value < 0)
            return (null, new MathError { Code = "negative_sqrt", Message = "Cannot take square root of a negative number." });

        var result = Math.Sqrt(req.Value);
        return (new MathResult { Result = result, Expression = $"√{req.Value} = {result}" }, null);
    }

    public (MathResult? Result, MathError? Error) Modulo(ModuloRequest req)
    {
        if (req.B == 0)
            return (null, new MathError { Code = "modulo_by_zero", Message = "Divisor (B) cannot be zero." });

        var result = req.A % req.B;
        return (new MathResult { Result = result, Expression = $"{req.A} mod {req.B} = {result}" }, null);
    }
}
