# Mcp.Governance

Operator-controlled policy layer. Decides which MCP tools are visible, at what
risk level, and which tenants are allowed to call them.

**Class library** — no executable, no NuGet package dependencies beyond ASP.NET Core.

---

## Responsibilities

| Concern | Owner |
|---|---|
| Authentication (who is the caller?) | Host web API (not this layer) |
| Authorisation (what can the caller do?) | Host web API (not this layer) |
| Tool exposure (is this tool enabled?) | `ExposureService` ← `mcp_exposure.json` |
| Risk gating (is the risk acceptable?) | `PolicyEngine` |
| Tenant restriction (is this tenant allowed?) | `PolicyEngine` |

Role and scope checks are intentionally excluded — the host API already enforces them
before requests reach the MCP layer.

---

## `mcp_exposure.json`

The single file you edit to control what the LLM can do. Place it in the working
directory of the host project.

```json
{
  "version": 1,
  "globalEnabled": true,
  "globalMaxRisk": "Medium",
  "tools": {
    "get_api_products": {
      "enabled": true,
      "risk": "ReadOnly",
      "allowedTenants": []
    },
    "post_api_orders": {
      "enabled": true,
      "risk": "Low",
      "allowedTenants": ["acme", "globex"]
    },
    "delete_api_data_id": {
      "enabled": false,
      "risk": "High",
      "allowedTenants": []
    }
  }
}
```

### Fields

| Field | Type | Description |
|---|---|---|
| `version` | `int` | Schema version (currently `1`) |
| `globalEnabled` | `bool` | Master switch — `false` disables all tools |
| `globalMaxRisk` | `RiskLevel` | Tool calls with higher risk are rejected |
| `tools` | `Dictionary<string, ToolPolicy>` | Per-tool policies keyed by tool name |

### `ToolPolicy` fields

| Field | Type | Description |
|---|---|---|
| `enabled` | `bool` | Must be `true` for the tool to appear in `tools/list` |
| `risk` | `RiskLevel` | Declared risk of this tool |
| `allowedTenants` | `string[]` | Empty = all tenants allowed; non-empty = allow-list |

---

## Risk levels

```
ReadOnly  →  Low  →  Medium  →  High
```

- `ReadOnly` — safe read-only queries; never triggers HITL.
- `Low` — lightweight writes; may trigger HITL depending on configuration.
- `Medium` — significant writes; typically requires HITL approval.
- `High` — destructive or irreversible operations; always blocked by default.

A tool call is blocked when `tool.risk > manifest.globalMaxRisk`.

The `AgentService` uses risk level to determine whether to pause and emit a
`HitlAgentEvent` before executing the tool.

---

## `ExposureService`

```csharp
public interface IExposureService
{
    bool TryGetEnabledPolicy(string toolName, out ToolPolicy policy);
}
```

- Returns `false` (and no policy) if the tool is absent from `mcp_exposure.json`.
- Returns `false` if `enabled: false`.
- Returns `false` if `globalEnabled: false`.
- Returns `true` + the policy otherwise.

Used by `DynamicMcpToolHandler` before executing any tool call.

---

## `PolicyEngine`

```csharp
public interface IPolicyEngine
{
    void EnforceToolPolicy(
        string toolName,
        ToolPolicy policy,
        RiskLevel globalMaxRisk,
        string? tenant);
}
```

Throws `GovernanceException` (403 equivalent) if:
- `policy.Risk > globalMaxRisk` — risk gate violated.
- `policy.AllowedTenants` is non-empty and `tenant` is not in the list — tenant blocked.

---

## `GovernanceException`

```csharp
// Thrown by PolicyEngine on policy violations
public sealed class GovernanceException : Exception
{
    public string ToolName { get; }
    public string Reason   { get; }   // "risk_gate" | "tenant_not_allowed"
    // ...
}
```

Caught by `DynamicMcpToolHandler` which converts it to an MCP error response.

---

## `McpExecutionContext`

Carries per-request metadata through the governance pipeline:

```csharp
public sealed class McpExecutionContext
{
    public string  ToolName      { get; }
    public string? Tenant        { get; }
    public string  CorrelationId { get; }
}
```

---

## `ICorrelationIdAccessor`

```csharp
public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}
```

The host registers the concrete implementation. `Mcp.Server.Http` uses
`HttpContextCorrelationIdAccessor` which reads `X-Correlation-Id` from the HTTP
request (or generates a new GUID if absent).

---

## Registration

```csharp
// Requires mcp_exposure.json in the working directory
services.AddGovernance(config);

// Register the host-specific ICorrelationIdAccessor separately
services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
```

`AddGovernance()` registers:
- `GovernanceOptions` (singleton)
- `ExposureManifest` (singleton, loaded from `mcp_exposure.json`)
- `IExposureService` / `ExposureService` (singleton)
- `IPolicyEngine` / `PolicyEngine` (singleton)

---

## Onboarding a new tool — zero code changes

1. Add an entry to `mcp_exposure.json`:

```json
"get_invoice_by_id": {
  "enabled": true,
  "risk": "ReadOnly",
  "allowedTenants": []
}
```

2. Restart the server.

The tool appears in `tools/list`, passes all governance checks, and flows through
to the invoker automatically.

---

## Project dependencies

```
Mcp.Governance
  └── Microsoft.AspNetCore.App  (framework reference only)
```

No NuGet package dependencies — intentionally kept minimal so it can be referenced
from any ASP.NET Core project without version conflicts.
