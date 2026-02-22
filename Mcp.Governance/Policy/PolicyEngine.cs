using Mcp.Governance.Execution;
using System.Security.Claims;

namespace Mcp.Governance.Policy;

public interface IPolicyEngine
{
    void EnforceToolPolicy(string toolName, ToolPolicy policy, ClaimsPrincipal principal, RiskLevel globalMaxRisk, string? tenant);
}

public sealed class PolicyEngine : IPolicyEngine
{
    public void EnforceToolPolicy(string toolName, ToolPolicy policy, ClaimsPrincipal principal, RiskLevel globalMaxRisk, string? tenant)
    {
        if (policy.Risk > globalMaxRisk)
        {
            throw GovernanceException.Forbidden(toolName, "risk_gate", "Tool is blocked by risk gate");
        }

        if (policy.RequiredRoles.Length > 0)
        {
            var ok = policy.RequiredRoles.Any(principal.IsInRole);
            if (!ok) throw GovernanceException.Forbidden(toolName, "missing_role", "Missing required role");
        }

        if (policy.RequiredScopes.Length > 0)
        {
            var scopes = GetScopes(principal);
            var ok = policy.RequiredScopes.All(s => scopes.Contains(s, StringComparer.OrdinalIgnoreCase));
            if (!ok) throw GovernanceException.Forbidden(toolName, "missing_scope", "Missing required scope");
        }

        if (policy.AllowedTenants.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(tenant) ||
                !policy.AllowedTenants.Contains(tenant, StringComparer.OrdinalIgnoreCase))
            {
                throw GovernanceException.Forbidden(toolName, "tenant_not_allowed", "Tenant is not allowed");
            }
        }
    }

    private static IReadOnlyCollection<string> GetScopes(ClaimsPrincipal principal)
    {
        var scope = principal.FindFirst("scope")?.Value;
        if (string.IsNullOrWhiteSpace(scope)) return Array.Empty<string>();
        return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}