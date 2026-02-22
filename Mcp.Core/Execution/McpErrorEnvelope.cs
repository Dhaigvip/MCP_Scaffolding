namespace Mcp.Core.Execution;

public class McpErrorEnvelope
{
    public bool Ok => false;
    public McpErrorDetail Error { get; }

    public McpErrorEnvelope(string code, string message)
    {
        Error = new McpErrorDetail(code, message);
    }
}

public record McpErrorDetail(string Code, string Message);

