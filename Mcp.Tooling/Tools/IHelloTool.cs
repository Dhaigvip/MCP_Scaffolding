namespace Mcp.Tooling.Tools;

public interface IHelloTool
{
    Task<object> SayHelloAsync(string name, CancellationToken ct);
}