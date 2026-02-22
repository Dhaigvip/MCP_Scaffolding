namespace Mcp.Governance.Policy;

public sealed class GovernanceOptions
{
    public RiskLevel DefaultMaxRisk { get; init; } = RiskLevel.ReadOnly;

    public Func<string?> TenantResolver { get; init; } = static () => null;
}