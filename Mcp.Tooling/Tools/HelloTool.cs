namespace Mcp.Tooling.Tools;

public sealed class HelloTool : IHelloTool
{
    public Task<string> SayHelloAsync(string name, CancellationToken ct)
        => Task.FromResult($"Hello, {name}!");
}
