namespace Mcp.Core.Governance;

public class McpGovernanceException : Exception
{
    public string Code { get; }

    public McpGovernanceException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}