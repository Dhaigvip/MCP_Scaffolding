namespace Mcp.Core.Registry;

using Mcp.Core.Abstractions;

public class ToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new();

    public void Register(IMcpTool tool)
    {
        _tools[tool.Name] = tool;
    }

    public IMcpTool? Get(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IEnumerable<IMcpTool> GetAll() => _tools.Values;
}