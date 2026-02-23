namespace Mcp.Tooling.Tools;

public interface IHelloTool
{
    Task<string> SayHelloAsync(string name, CancellationToken ct);
}
