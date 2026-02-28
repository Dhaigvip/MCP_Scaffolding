# Mcp.Palma.Contracts

Zero-dependency shared contract library. Contains only the types that must be visible
to **both** the Palma web API and the MCP pipeline — with no dependency on either.

---

## Why this project exists

There are two deployment scenarios where the Palma web API and the MCP server need to
share types:

| Scenario | What is shared |
|---|---|
| **In-process (Scenario 2)** | Palma implements `IPalmaEndpointSource` + `IPalmaToolInvoker` |
| **Separate process (Scenario 3)** | Palma implements `IPalmaEndpointSource` (serialised over HTTP) |

Placing these contracts in a standalone project with **no NuGet dependencies** keeps the
Palma web API from pulling in the full MCP SDK.

---

## Contents

### `PalmaContext`

Per-session routing context supplied by the React client at session creation.
Injected into every tool invocation — never exposed in tool input schemas.

```csharp
public sealed record PalmaContext(
    string  Version,   // API version → /API/v{Version}/...
    string  Org,       // query param: org
    string? Ms,        // query param: ms  (optional)
    string? Branch);   // query param: branch (optional)
```

**Example value:**
```json
{ "version": "1", "org": "acme-corp", "ms": "erp", "branch": "main" }
```

---

### `IPalmaEndpointSource`

Implemented by the Palma web API. Called by `PalmaToolLoader` (in-process) or
serialised to JSON for `PalmaRemoteToolLoader` (HTTP).

```csharp
public interface IPalmaEndpointSource
{
    IEnumerable<PalmaEndpointInfo> GetEndpoints();
}
```

**Minimal implementation:**

```csharp
public class PalmaEndpointSourceImpl : IPalmaEndpointSource
{
    public IEnumerable<PalmaEndpointInfo> GetEndpoints()
    {
        yield return new PalmaEndpointInfo
        {
            Uri         = "GET/modules",
            Summary     = "List all modules for the given org and module system",
            QueryParams =
            [
                new PalmaQueryParamInfo { Name = "org",    Required = true,  Type = "string", Description = "Organisation ID" },
                new PalmaQueryParamInfo { Name = "ms",     Required = false, Type = "string", Description = "Module system" },
                new PalmaQueryParamInfo { Name = "branch", Required = false, Type = "string", Description = "Branch name" }
            ]
        };
    }
}
```

---

### `PalmaEndpointInfo`

Lightweight DTO describing one Palma endpoint. No Palma-internal types.

```csharp
public sealed class PalmaEndpointInfo
{
    public required string           Uri         { get; init; }  // e.g. "GET/modules"
    public string                    HttpMethod  { get; init; } = "POST";
    public string?                   Summary     { get; init; }
    public string?                   Description { get; init; }
    public List<PalmaQueryParamInfo> QueryParams { get; init; } = [];
}
```

The `Uri` field uses the Palma convention: `"{HTTP_METHOD}/{path}"`, e.g.
`"GET/modules"`, `"POST/module"`, `"GET/module/versions"`.

`PalmaToolBuilder` converts this to an MCP tool name: `"GET/modules"` → `"get_modules"`.

---

### `PalmaQueryParamInfo`

Describes one query parameter accepted by a Palma endpoint.

```csharp
public sealed class PalmaQueryParamInfo
{
    public required string Name         { get; init; }
    public bool            Required     { get; init; }
    public string          Type         { get; init; } = "string";
    public string?         Description  { get; init; }
    public string?         ExampleValue { get; init; }
}
```

**Context parameters** (e.g. `org`, `ms`, `branch`) are listed here but are
stripped from the MCP tool schema by `PalmaToolBuilder`. The LLM never sees them —
they are injected automatically from `PalmaContext` at invocation time.

---

## Usage by scenario

### Scenario 2 — In-process

```xml
<!-- Palma.WebApi.csproj -->
<ProjectReference Include="..\Palma_MCP\Mcp.Palma.Contracts\Mcp.Palma.Contracts.csproj" />
<ProjectReference Include="..\Palma_MCP\Mcp.Server\Mcp.Server.csproj" />
```

Palma implements both `IPalmaEndpointSource` and `IPalmaToolInvoker` (defined in
`Mcp.Server`) and registers them before calling `AddPalmaMcp()`.

### Scenario 3 — Remote

```xml
<!-- Palma.WebApi.csproj -->
<ProjectReference Include="..\Palma_MCP\Mcp.Palma.Contracts\Mcp.Palma.Contracts.csproj" />
```

Palma implements `IPalmaEndpointSource` and exposes it via `GET /api/mcp/endpoints`.
The MCP server calls that endpoint over HTTP — no other MCP project reference needed.
