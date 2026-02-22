namespace Mcp.Governance.Policy;

public sealed class ToolPolicy
{
    public bool Enabled { get; init; }

    public RiskLevel Risk { get; init; } = RiskLevel.ReadOnly;

    public string[] RequiredRoles { get; init; } = Array.Empty<string>();

    public string[] RequiredScopes { get; init; } = Array.Empty<string>();

    public string[] AllowedTenants { get; init; } = Array.Empty<string>();
}