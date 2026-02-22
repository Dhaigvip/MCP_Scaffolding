namespace Mcp.Tooling.Tools;

public sealed class HelloTool : IHelloTool
{
    public Task<object> SayHelloAsync(string name, CancellationToken ct)
    {
        object result = new
        {
            message = $"Hello, {name}"
        };

        return Task.FromResult(result);
    }
}