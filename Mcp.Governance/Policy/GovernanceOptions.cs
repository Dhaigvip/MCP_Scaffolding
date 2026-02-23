namespace Mcp.Governance.Policy;

/// <summary>
/// Governance configuration bound from appsettings.json under "Governance".
/// ScopeClaimName removed — scope enforcement is the host WebAPI's concern.
/// </summary>
public sealed class GovernanceOptions
{
    public RiskLevel DefaultMaxRisk { get; init; } = RiskLevel.ReadOnly;

    /// <summary>
    /// Resolves the current tenant from ambient context (header, config, etc).
    /// Only relevant if any tool declares AllowedTenants in mcp_exposure.json.
    /// </summary>
    public Func<string?> TenantResolver { get; init; } = static () => null;
}