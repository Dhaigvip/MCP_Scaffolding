using System;

namespace Mcp.Governance.Policy;

/// <summary>
/// Per-tool policy declared in mcp_exposure.json.
/// RequiredRoles and RequiredScopes removed — enforced by host WebAPI, not here.
/// </summary>
public sealed class ToolPolicy
{
    public bool Enabled { get; init; }

    public RiskLevel Risk { get; init; } = RiskLevel.ReadOnly;

    /// <summary>
    /// Optional allow-list of tenant identifiers.
    /// Empty means all tenants are permitted.
    /// </summary>
    public string[] AllowedTenants { get; init; } = Array.Empty<string>();
}