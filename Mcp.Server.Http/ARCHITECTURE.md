# MCP Server — Dynamic Tool Exposure from Swagger

## Overview

This MCP server automatically exposes your existing WebAPI endpoints as MCP tools
by reading your Swagger/OpenAPI spec at startup. There are no manually written
`XArgs`, `XToolHandler`, or `XToolAdapter` classes. The only things you maintain are:

- **`[McpTool]`** attribute on your controller methods — becomes the tool description
- **`[Description]`** attribute on your request model properties — becomes each parameter description
- **`mcp_exposure.json`** — the operator control plane that decides which tools are visible and at what risk level

Authentication and authorization are handled entirely by the host WebAPI. This MCP server
sits behind it and trusts that only permitted requests reach it.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                          YOUR WEB API                                │
│                                                                      │
│  MathController                                                      │
│    [McpTool("Adds two numbers...")]                                  │
│    POST /api/math/add                                                │
│      AddRequest                                                      │
│        [Description("First number...")] double A                     │
│        [Description("Second number...")] double B                    │
│                                                                      │
│  Swashbuckle reads attributes → writes into swagger.json             │
│    McpToolOperationFilter  → operation.summary                       │
│    [Description]           → property.description  (native support) │
│                                                                      │
│  GET /swagger/v1/swagger.json  ◄── served here                       │
└───────────────────────────┬──────────────────────────────────────────┘
                            │ HTTP fetch at startup
                            ▼
┌──────────────────────────────────────────────────────────────────────┐
│                         MCP SERVER                                   │
│                                                                      │
│  STARTUP                                                             │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ SwaggerMcpStartupService (IHostedService)                   │    │
│  │   └── SwaggerToolLoader.LoadAsync()                         │    │
│  │         ├── Fetches swagger.json                            │    │
│  │         ├── Parses every path + method                      │    │
│  │         ├── Resolves $ref schemas                           │    │
│  │         ├── Expands body properties into inputSchema        │    │
│  │         └── Produces SwaggerToolDescriptor[]                │    │
│  │                                                             │    │
│  │   └── DynamicToolRegistry.Initialize()                     │    │
│  │         ├── Filters against mcp_exposure.json               │    │
│  │         └── Stores enabled tools only                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                      │
│  RUNTIME                                                             │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ MCP Client request                                          │    │
│  │   │                                                         │    │
│  │   ├── tools/list                                            │    │
│  │   │     └── DynamicMcpToolHandler.ListToolsAsync()          │    │
│  │   │           └── DynamicToolRegistry.All                   │    │
│  │   │                 → Tool[] with name, description, schema │    │
│  │   │                                                         │    │
│  │   └── tools/call                                            │    │
│  │         └── DynamicMcpToolHandler.CallToolAsync()           │    │
│  │               ├── ExposureService  (enabled check)          │    │
│  │               ├── PolicyEngine     (risk + tenant gates)    │    │
│  │               └── SwaggerToolInvoker.InvokeAsync()          │    │
│  │                     ├── Substitutes path params             │    │
│  │                     ├── Appends query params                │    │
│  │                     ├── Reconstructs JSON body              │    │
│  │                     └── HTTP → Your WebAPI                  │    │
│  └─────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Component Reference

### WebAPI Side

#### `[McpTool]` attribute — `Mcp/McpAttributes.cs`
Decorates a controller action. Its string becomes the tool `description` shown to LLM clients.
Write it for the model, not a developer — explain when to use it, give examples, mention error codes.

```csharp
[McpTool("Adds two numbers and returns their sum. Use when combining values or calculating a total. Example: A=15, B=27 returns 42.")]
[HttpPost("add", Name = "AddNumbers")]
public IActionResult Add([FromBody] AddRequest request) { ... }
```

#### `[Description]` attribute — `System.ComponentModel`
Decorates request model properties. Swashbuckle reads this natively — no schema filter needed.
Be specific: state valid ranges, constraints, what the value means, and what errors a bad value causes.

```csharp
public sealed class DivideRequest
{
    [Description("The dividend — the number to be divided.")]
    public double A { get; init; }

    [Description("The divisor — must not be zero or a division_by_zero error is returned.")]
    public double B { get; init; }
}
```

#### `McpToolOperationFilter` — `Mcp/McpSwaggerFilters.cs`
Swashbuckle `IOperationFilter` that reads `[McpTool]` and writes its text into
`operation.summary` and `operation.description` in swagger.json. Registered in `Program.cs`:

```csharp
c.OperationFilter<McpToolOperationFilter>();
```

> **Why `[Description]` for properties but `[McpTool]` for methods?**
> Swashbuckle reads `[Description]` on model properties natively with no configuration.
> For controller methods it does not — so `McpToolOperationFilter` bridges that gap.
> `McpParamSchemaFilter` was tried but abandoned: Swashbuckle's `context.MemberInfo`
> is null for `$ref`-resolved schemas, making it unreliable.

---

### MCP Server Side

#### `SwaggerMcpOptions`
Two required config values in `appsettings.json`:

```json
"SwaggerMcp": {
  "SwaggerUrl": "http://localhost:7001/swagger/v1/swagger.json",
  "ApiBaseUrl": "http://localhost:7001"
}
```

- **SwaggerUrl** — fetched once at startup to discover tools
- **ApiBaseUrl** — prepended to every outbound HTTP call at runtime

---

#### `SwaggerMcpStartupService`
Implements `IHostedService`. `StartAsync` is awaited before the server accepts requests,
guaranteeing the tool registry is fully populated before any `tools/list` or `tools/call`
arrives. If swagger.json is unreachable it logs an error and starts with zero tools
rather than crashing.

```
StartAsync()
  └── SwaggerToolLoader.LoadAsync(swaggerUrl)
        └── DynamicToolRegistry.Initialize(tools, exposureService)
```

> **Why `IHostedService` not `IStartupFilter`?**
> `IStartupFilter` was tried first but is silently skipped in the .NET 8+ minimal
> hosting model — the loader never ran. `IHostedService.StartAsync` is always called
> and awaited reliably before the HTTP pipeline opens.

---

#### `SwaggerToolLoader`
Fetches and parses swagger.json into `SwaggerToolDescriptor` objects.

**Tool name:** uses `operationId` as-is (preserving casing like `AddNumbers`).
If no `operationId` exists, falls back to `{method}_{path_segments}` e.g. `get_api_orders_id`.

**Schema expansion:** follows `$ref` pointers into `components/schemas`, resolves the
actual request type, and expands its properties directly into the MCP `inputSchema`.
This means the LLM sees individual typed fields, not an opaque `body: object`.

**Before expansion (wrong):**
```json
"inputSchema": {
  "properties": { "body": { "type": "object" } }
}
```

**After expansion (correct):**
```json
"inputSchema": {
  "properties": {
    "a": { "type": "number", "description": "The dividend..." },
    "b": { "type": "number", "description": "The divisor..." }
  },
  "required": ["a", "b"]
}
```

---

#### `SwaggerToolDescriptor`
Immutable data object representing one parsed API operation. Holds everything needed
to describe and execute the tool:

```
SwaggerToolDescriptor
  ├── ToolName        "AddNumbers"
  ├── Description     "Adds two numbers and returns their sum..."
  ├── HttpMethod      "POST"
  ├── PathTemplate    "/api/math/add"
  ├── Parameters      []   (no path/query params for this endpoint)
  ├── Body            { Required: true, Schema: <AddRequest schema> }
  └── InputSchema     { properties: { a: number, b: number }, required: [a, b] }
```

---

#### `DynamicToolRegistry`
Singleton in-memory store. Populated once at startup, read-only at runtime.
Acts as the join point between what your API offers and what is permitted to be exposed.

**Filter logic at initialization:**
```
For each SwaggerToolDescriptor:
    if toolName exists in mcp_exposure.json AND enabled = true
        → store in registry
    else
        → skip, log as debug
```

---

#### `DynamicMcpToolHandler`
The single class that handles all MCP protocol traffic.
Registered via the SDK's custom handler hooks — no `[McpServerToolType]` attributes,
no per-tool classes, no code generation.

```csharp
builder.Services.AddMcpServer()
    .WithListToolsHandler((ctx, ct) => handler.ListToolsAsync(ctx, ct))
    .WithCallToolHandler((ctx, ct) => handler.CallToolAsync(ctx, ct));
```

**`tools/list`:** maps `DynamicToolRegistry.All` → `Tool[]` with name, description, inputSchema.

**`tools/call`:** runs the governance pipeline then delegates to `SwaggerToolInvoker`:
1. `ExposureService.TryGetEnabledPolicy()` — is the tool enabled?
2. `PolicyEngine.EnforceToolPolicy()` — risk gate + tenant gate
3. `DynamicToolRegistry.TryGet()` — resolve the descriptor
4. `SwaggerToolInvoker.InvokeAsync()` — call the real API
5. Return `CallToolResult` with `TextContentBlock` containing the JSON response

> **Lifetime:** Scoped (not Singleton) because it depends on `ICorrelationIdAccessor`
> which is Scoped (reads per-request from `IHttpContextAccessor`). Resolved via
> `CreateScope()` inside the handler lambdas in `Program.cs`.

---

#### `SwaggerToolInvoker`
Translates a tool call into a real HTTP request to your WebAPI.

**Path parameter substitution:**
```
PathTemplate:  /api/orders/{id}
Arguments:     { "id": "42" }
Result URL:    /api/orders/42
```

**Query string assembly:**
```
PathTemplate:  /api/orders
Arguments:     { "status": "active", "page": "2" }
Result URL:    /api/orders?status=active&page=2
```

**Body reconstruction — key design decision:**
Because the schema is expanded, the LLM sends individual properties (`a`, `b`) not
a wrapped `body` object. The invoker collects all arguments that are NOT path or query
parameters and serializes them back into a JSON object for the request body.

```
Arguments received:  { "a": 15, "b": 27 }
Path/query params:   none
Body sent to API:    { "a": 15, "b": 27 }  +  Content-Type: application/json
```

If no body arguments are present, sends `{}` to prevent 415 Unsupported Media Type —
which occurs when a POST is sent without a `Content-Type` header.

**Correlation ID forwarding:**
Every outbound request carries `X-Correlation-Id` enabling end-to-end tracing
across the MCP server and your WebAPI logs.

---

#### `PolicyEngine`
Enforces what this MCP layer owns — not auth (that's the host WebAPI's job):

- **Risk gate** — tool's declared `risk` must not exceed `globalMaxRisk`
- **Tenant gate** — if `allowedTenants` is set, the resolved tenant must be in the list

```
RiskLevel: ReadOnly(0) < Low(1) < Medium(2) < High(3)
```

---

#### `mcp_exposure.json`
The operator control plane. The only file you edit to onboard, restrict, or disable tools.

```json
{
  "version": 1,
  "globalEnabled": true,
  "globalMaxRisk": "Medium",
  "tools": {
    "AddNumbers": {
      "enabled": true,
      "risk": "ReadOnly",
      "allowedTenants": []
    },
    "DivideNumbers": {
      "enabled": true,
      "risk": "Low",
      "allowedTenants": ["tenant-a"]
    },
    "DeleteOrder": {
      "enabled": false,
      "risk": "High",
      "allowedTenants": []
    }
  }
}
```

`DeleteOrder` is doubly blocked: `enabled: false` AND `risk: High` exceeds the
`globalMaxRisk` of `Medium`. Either condition alone would block it.

---

## End-to-End Request Flow

```
Postman / LLM Client
  │
  │  POST /api/mcp
  │  Mcp-Session-Id: <session>
  │  {
  │    "jsonrpc": "2.0",
  │    "method": "tools/call",
  │    "params": {
  │      "name": "AddNumbers",
  │      "arguments": { "a": 15, "b": 27 }
  │    }
  │  }
  │
  ▼
MCP SDK  →  WithCallToolHandler lambda
  │
  ▼
DynamicMcpToolHandler.CallToolAsync()
  ├── ExposureService.TryGetEnabledPolicy("AddNumbers")
  │     └── manifest.Tools["AddNumbers"].Enabled = true  ✓
  │
  ├── PolicyEngine.EnforceToolPolicy()
  │     ├── risk ReadOnly(0) ≤ globalMaxRisk Medium(2)  ✓
  │     └── allowedTenants = []  (unrestricted)  ✓
  │
  ├── DynamicToolRegistry.TryGet("AddNumbers")
  │     └── descriptor: POST /api/math/add, body props: {a, b}
  │
  ▼
SwaggerToolInvoker.InvokeAsync()
  ├── BuildUrl()       →  /api/math/add
  ├── BuildRequest()
  │     ├── Method:         POST
  │     ├── Body:           {"a":15,"b":27}
  │     ├── Content-Type:   application/json
  │     └── X-Correlation-Id: abc-123
  │
  ▼
Math WebAPI  →  POST http://localhost:7001/api/math/add
  │              Body: {"a":15,"b":27}
  │
  ◄──  200 OK  { "result": 42, "expression": "15 + 27 = 42" }
  │
SwaggerToolInvoker  →  returns JSON string
  │
DynamicMcpToolHandler
  └── CallToolResult {
        Content: [ TextContentBlock { Text: '{"result":42,"expression":"15 + 27 = 42"}' } ]
      }
  │
MCP SDK  →  JSON-RPC response
  │
  ◄── {
        "result": {
          "content": [{ "type": "text", "text": "{\"result\":42,\"expression\":\"15 + 27 = 42\"}" }]
        },
        "id": 3,
        "jsonrpc": "2.0"
      }
```

---

## Onboarding a New Endpoint — Zero Code in MCP Project

Say your WebAPI adds `POST /api/invoices` for creating an invoice.

**Step 1 — Decorate the WebAPI endpoint:**
```csharp
[McpTool("Creates a new invoice. Use when you need to raise an invoice for a customer. Returns the created invoice with its ID and status.")]
[HttpPost("invoices", Name = "CreateInvoice")]
public IActionResult Create([FromBody] CreateInvoiceRequest request) { ... }

public sealed class CreateInvoiceRequest
{
    [Description("The customer ID to invoice. Must be an existing customer.")]
    public string CustomerId { get; init; } = string.Empty;

    [Description("The total amount in the account's base currency. Must be greater than zero.")]
    public decimal Amount { get; init; }
}
```

**Step 2 — Add one entry to `mcp_exposure.json`:**
```json
"CreateInvoice": {
  "enabled": true,
  "risk": "Low",
  "allowedTenants": []
}
```

**Step 3 — Restart the MCP server.**

The tool appears in `tools/list` with full description and typed parameters.
The LLM can call it. It flows through the full governance pipeline automatically.
No changes anywhere in the MCP project.

---

## Startup Log Reference

```
info  SwaggerMcpStartupService  Loading tools from Swagger at http://localhost:7001/swagger/v1/swagger.json
info  SwaggerToolLoader          Registered tool [AddNumbers] ← POST /api/math/add | 2 input properties
info  SwaggerToolLoader          Registered tool [SubtractNumbers] ← POST /api/math/subtract | 2 input properties
info  SwaggerToolLoader          Registered tool [MultiplyNumbers] ← POST /api/math/multiply | 2 input properties
info  SwaggerToolLoader          Registered tool [DivideNumbers] ← POST /api/math/divide | 2 input properties
info  SwaggerToolLoader          Registered tool [PowerNumbers] ← POST /api/math/power | 2 input properties
info  SwaggerToolLoader          Registered tool [SquareRoot] ← POST /api/math/sqrt | 1 input properties
info  SwaggerToolLoader          Registered tool [ModuloNumbers] ← POST /api/math/modulo | 2 input properties
info  SwaggerToolLoader          Loaded 7 tools from OpenAPI spec.
debug DynamicToolRegistry        Tool DeleteOrder skipped — not in exposure manifest or disabled.
info  DynamicToolRegistry        Tool AddNumbers activated: POST /api/math/add
info  DynamicToolRegistry        7 tools active after exposure filtering.
```

---

## Testing with Postman

All requests go to `POST http://localhost:5104/api/mcp` with headers:

| Header | Value |
|---|---|
| Content-Type | application/json |
| Mcp-Session-Id | `{{mcp_session_id}}` |

**1 — Initialize (run first, captures session ID):**
```json
{ "jsonrpc": "2.0", "id": 1, "method": "initialize",
  "params": { "protocolVersion": "2024-11-05", "capabilities": {},
               "clientInfo": { "name": "postman", "version": "1.0" } } }
```
Tests tab script:
```javascript
if (pm.response && pm.response.headers) {
    var sessionId = pm.response.headers.get("Mcp-Session-Id");
    if (sessionId) pm.collectionVariables.set("mcp_session_id", sessionId);
}
```

**2 — List tools:**
```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {} }
```

**3 — Call a tool:**
```json
{
  "jsonrpc": "2.0", "id": 3, "method": "tools/call",
  "params": {
    "name": "AddNumbers",
    "arguments": { "a": 15, "b": 27 }
  }
}
```

**Expected response:**
```json
{
  "result": {
    "content": [{ "type": "text", "text": "{\"result\":42.0,\"expression\":\"15 + 27 = 42\"}" }]
  },
  "id": 3,
  "jsonrpc": "2.0"
}
```

---

## Common Errors and Fixes

| Error | Cause | Fix |
|---|---|---|
| `tools: []` on startup | `IStartupFilter` silently skipped | Use `IHostedService` — `SwaggerMcpStartupService` |
| `tool_not_exposed` | Tool name in `mcp_exposure.json` doesn't match `operationId` | Check exact casing — names are case-sensitive |
| `415 Unsupported Media Type` | POST sent without `Content-Type: application/json` | Invoker now always sets content type, sends `{}` if no body args |
| `description: null` on properties | `McpParamSchemaFilter` not firing for `$ref` schemas | Use `[Description]` from `System.ComponentModel` instead |
| Scoped from Singleton error | `DynamicMcpToolHandler` registered as Singleton consuming Scoped `ICorrelationIdAccessor` | Register handler as Scoped, resolve via `CreateScope()` in lambdas |
| `Cannot convert Task to ValueTask` | SDK handler hooks expect `ValueTask<T>` | Return `ValueTask<T>` from handler methods |