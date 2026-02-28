using Mcp.Governance.Execution;
using System;
using System.Linq;

namespace Mcp.Governance.Policy;

public interface IPolicyEngine
{
    void EnforceToolPolicy(
        string toolName,
        ToolPolicy policy,
        RiskLevel globalMaxRisk,
        string? tenant);
}

/// <summary>
/// Enforces only what this MCP layer owns:
///   - Risk gate   : tool's declared risk must not exceed the global ceiling
///   - Tenant gate : tool may restrict which tenants can call it
///
/// Role and scope checks are intentionally removed — the host WebAPI
/// already enforces authentication and authorization before requests
/// reach this MCP server.
/// </summary>
public sealed class PolicyEngine : IPolicyEngine
{
    public void EnforceToolPolicy(
        string toolName,
        ToolPolicy policy,
        RiskLevel globalMaxRisk,
        string? tenant)
    {
        if (policy.Risk > globalMaxRisk)
            throw GovernanceException.Forbidden(toolName, "risk_gate", "Tool is blocked by risk gate");

        if (policy.AllowedTenants.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(tenant) ||
                !policy.AllowedTenants.Contains(tenant, StringComparer.OrdinalIgnoreCase))
            {
                throw GovernanceException.Forbidden(toolName, "tenant_not_allowed", "Tenant is not allowed");
            }
        }
    }
}