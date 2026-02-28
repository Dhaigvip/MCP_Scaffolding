# Mcp.Server

Core MCP pipeline library. Provides the tool loading, registration, and invocation
abstractions that power all three deployment scenarios.

**This is a class library — it has no `Program.cs` and cannot run standalone.**
It is consumed by `Mcp.Server.Http` (standalone host) or by the Palma web API
(in-process hosting).

---

## Architecture

```
                    IToolLoader
                        │
          ┌─────────────┼─────────────────┐
          │             │                 │
  SwaggerToolLoader  PalmaToolLoader  PalmaRemoteToolLoader
  (fetches           (reads            (calls GET /api/mcp/
   swagger.json)      IPalmEndpointSource) endpoints over HTTP)
          │             │                 │
          └─────────────┼─────────────────┘
                        │
                 SwaggerMcpStartupService
                  (HostedService, runs on startup)
                        │
                 DynamicToolRegistry
                  (in-memory, volatile-backed, atomic swap)
                        │
                 DynamicMcpToolHandler
                  (handles tools/list + tools/call)
                        │
                    IToolInvoker
                        │
          ┌─────────────┼──────────────────┐
          │             │                  │
  SwaggerToolInvoker  PalmaToolInvoker  PalmaRemoteToolInvoker
  (HTTP calls to       (in-process via    (HTTP POST to Palma
   external API)        IPalmaToolInvoker)  with OAuth2 auth)
```

---

## Key abstractions

### `IToolLoader`

```csharp
public interface IToolLoader
{
    Task<List<SwaggerToolDescriptor>> LoadAsync(CancellationToken ct = default);
}
```

Called once at startup by `SwaggerMcpStartupService`. Returns the full list of
tools discovered from the backend (Swagger spec or Palma endpoint list).

### `IToolInvoker`

```csharp
public interface IToolInvoker
{
    Task<string> InvokeAsync(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        PalmaContext? palmaContext,
        string correlationId,
        CancellationToken ct);
}
```

Called at runtime for each tool invocation. Returns the raw JSON result string.

---

## Concrete implementations

### Swagger pipeline (Scenario 1)

#### `SwaggerToolLoader`

- Fetches `swagger.json` from `SwaggerMcp:SwaggerUrl` at startup.
- Walks every `path → method` combination.
- Derives tool names from `operationId` (slugified) or `method + path`.
- Extracts path params, query params, and request body schema.

#### `SwaggerToolInvoker`

- Substitutes path parameters into the URL template.
- Appends query parameters.
- Serialises the `body` key as `application/json` for POST/PUT/PATCH.
- Forwards `X-Correlation-Id` to the upstream API.

### Palma in-process pipeline (Scenario 2)

#### `PalmaToolLoader`

- Calls `IPalmaEndpointSource.GetEndpoints()` directly in-process (no HTTP).
- Delegates to `PalmaToolBuilder.Build()`.

#### `PalmaToolInvoker`

- Pure adapter: `IToolInvoker → IPalmaToolInvoker`.
- Extracts query parameter values from `JsonElement` arguments.
- Strips context params — those come from `PalmaContext`.
- Calls `IPalmaToolInvoker.InvokeAsync(uri, context, additionalParams, ct)`.
- Zero HTTP overhead.

#### `IPalmaToolInvoker` (interface — implemented by Palma web API)

```csharp
public interface IPalmaToolInvoker
{
    Task<string> InvokeAsync(
        string uri,
        PalmaContext context,
        IReadOnlyDictionary<string, string> additionalParams,
        CancellationToken ct);
}
```

### Palma remote pipeline (Scenario 3)

#### `PalmaTokenProvider`

- Singleton. Obtains an OAuth2 token via `client_credentials` grant.
- Caches the token; re-fetches 60 seconds before expiry.
- Thread-safe: uses `SemaphoreSlim` with double-checked locking.

#### `PalmaRemoteToolLoader`

- Calls `GET {ApiBaseUrl}{EndpointsPath}` with `Authorization: Bearer` + `AuthScheme` headers.
- Deserialises `List<PalmaEndpointInfo>`.
- Delegates to `PalmaToolBuilder.Build()`.

#### `PalmaRemoteToolInvoker`

- Constructs `POST /API/v{version}/{uri}?org=...&ms=...&branch=...&{llm-params}`.
- Adds `Authorization: Bearer`, `AuthScheme`, and `X-Correlation-Id` headers.
- Returns the raw JSON response or an error envelope on non-2xx.

### Shared helper

#### `PalmaToolBuilder`

Static helper used by both `PalmaToolLoader` and `PalmaRemoteToolLoader`.

- Filters out context parameters (`ContextQueryParams`) from the tool schema.
- Converts `"GET/modules"` to tool name `"get_modules"`.
- Builds `JsonSchema` with properties and required list.

---

## `DynamicToolRegistry`

Singleton. Holds the filtered set of active tools after startup.

- Backed by a `volatile` field — read access needs no lock.
- Written via `Interlocked.Exchange` — atomic swap means readers never see a
  partial state.
- `Initialize(tools, exposureService)` applies the `mcp_exposure.json` allow-list.
- `TryGet(toolName)` — O(1) dictionary lookup used by the handler at runtime.

---

## `DynamicMcpToolHandler`

Scoped. The single class that handles all MCP protocol traffic.

**`tools/list`** — maps `DynamicToolRegistry.All` to MCP `Tool` objects with name,
description, and JSON Schema.

**`tools/call`** — full governance pipeline:
1. `ExposureService.TryGetEnabledPolicy()` — tool must exist and be enabled.
2. `PolicyEngine.EnforceToolPolicy()` — risk gate and tenant gate.
3. `DynamicToolRegistry.TryGet()` — fetch descriptor.
4. `IToolInvoker.InvokeAsync()` — execute and return.

---

## `SwaggerMcpStartupService`

`IHostedService`. Runs `IToolLoader.LoadAsync()` on application startup, before
the HTTP pipeline accepts requests. Populates `DynamicToolRegistry`.

---

## Registration

Use one of the three extension methods in `ServiceCollectionExtensions`:

```csharp
// Scenario 1 — Swagger
services.AddSwaggerMcp(config);

// Scenario 2 — Palma in-process
// (IPalmaEndpointSource + IPalmaToolInvoker must already be registered)
services.AddPalmaMcp(config);

// Scenario 3 — Palma remote (OAuth2)
services.AddPalmaMcpRemote(config);
```

Then add the MCP SDK handlers (required in all scenarios):

```csharp
services.AddMcpServerHandlers();
```

All three `Add*Mcp` methods call the private `AddMcpCore()` which registers:
- `DynamicToolRegistry` (singleton)
- `ToolRefreshService` (singleton)
- `DynamicMcpToolHandler` (scoped)
- `SwaggerMcpStartupService` (hosted service)

---

## `SwaggerToolDescriptor`

The shared data transfer object flowing between loader and invoker:

```
SwaggerToolDescriptor
  ├── ToolName        "get_modules"
  ├── Description     "List all modules"
  ├── HttpMethod      "GET"
  ├── PathTemplate    "GET/modules"  (Palma) or "/api/orders/{id}" (Swagger)
  ├── Parameters      [ { Name, In, Type, Description, Required } ]
  ├── Body            null | { ... }
  └── InputSchema     { type: "object", properties: {...}, required: [...] }
```

---

## Project dependencies

```
Mcp.Server
  ├── Mcp.Governance          (ExposureService, PolicyEngine)
  ├── Mcp.Palma.Contracts     (PalmaContext, IPalmaEndpointSource, DTOs)
  ├── ModelContextProtocol    (MCP SDK)
  └── ModelContextProtocol.AspNetCore
```
