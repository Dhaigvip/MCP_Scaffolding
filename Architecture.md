# Dynamic MCP Tools from Swagger — How It All Works

## Overview

Instead of manually writing `XArgs`, `XToolHandler`, and `XToolAdapter` classes for every
Web API endpoint, this pipeline reads your existing **swagger.json** at startup and
automatically registers every permitted operation as an MCP tool. The only thing you
maintain is `mcp_exposure.json` — a simple allow-list that controls which endpoints
are visible to LLM clients and at what risk level.

---

## The Big Picture

```
┌─────────────────────────────────────────────────────────────┐
│                        STARTUP                              │
│                                                             │
│  mcp_exposure.json ──────────────────────┐                  │
│                                          ▼                  │
│  swagger.json ──► SwaggerToolLoader ──► DynamicToolRegistry │
│                                                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ (populated before first request)
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        RUNTIME                              │
│                                                             │
│  MCP Client                                                 │
│     │                                                       │
│     ├── tools/list ──► DynamicMcpToolHandler                │
│     │                       │                               │
│     │                       └──► DynamicToolRegistry.All    │
│     │                                                       │
│     └── tools/call ──► DynamicMcpToolHandler                │
│                             │                               │
│                             ├── GovernanceExecutor          │
│                             │     ├── Exposure gate         │
│                             │     ├── Risk gate             │
│                             │     └── Tenant gate           │
│                             │                               │
│                             └── SwaggerToolInvoker          │
│                                   └──► Your WebAPI (HTTP)   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Components

### 1. `SwaggerMcpOptions`
Configuration loaded from `appsettings.json`. Two values are required:

```json
"SwaggerMcp": {
  "SwaggerUrl": "https://localhost:7001/swagger/v1/swagger.json",
  "ApiBaseUrl": "https://localhost:7001"
}
```

- **SwaggerUrl** — where to fetch the OpenAPI spec from at startup
- **ApiBaseUrl** — base URL prepended to every HTTP call the invoker makes at runtime

---

### 2. `SwaggerToolLoader`
Runs once at startup. Fetches `swagger.json` over HTTP and walks every `path → method`
combination, turning each one into a `SwaggerToolDescriptor`.

**Tool name derivation:**
- If the operation has an `operationId` → slugified: `GetOrderById` → `get_order_by_id`
- If not → built from method + path: `GET /api/orders/{id}` → `get_api_orders_id`

**What it extracts per operation:**
- Tool name and description (from `summary` or `description`)
- HTTP method and path template
- Path parameters, query parameters
- Request body schema (OpenAPI 3.x `requestBody`)
- A combined JSON Schema for the MCP `InputSchema` (what the LLM sees)

---

### 3. `SwaggerToolDescriptor`
A plain data object representing one parsed API operation. Contains everything needed
to both describe the tool to an LLM client and execute it against the real API:

```
SwaggerToolDescriptor
  ├── ToolName        "get_order_by_id"
  ├── Description     "Returns an order by its ID"
  ├── HttpMethod      "GET"
  ├── PathTemplate    "/api/orders/{id}"
  ├── Parameters      [ { Name: "id", In: "path", Type: "string", Required: true } ]
  ├── Body            null  (GET has no body)
  └── InputSchema     { type: "object", properties: { id: { type: "string" } }, required: ["id"] }
```

---

### 4. `DynamicToolRegistry`
A singleton in-memory store populated once at startup. Holds only the tools that passed
the exposure filter. All runtime lookups go through here.

**Initialization filter logic:**
```
For each tool from SwaggerToolLoader:
    if tool name exists in mcp_exposure.json AND enabled = true
        → add to registry
    else
        → skip (logged as debug)
```

This is the join point between what your API offers and what you have explicitly
permitted to be exposed as MCP tools.

---

### 5. `SwaggerMcpStartupFilter`
Implements `IStartupFilter` — runs synchronously before the HTTP pipeline opens.
This guarantees the registry is fully populated before the first MCP request arrives.

```
App startup
    └── SwaggerMcpStartupFilter.Configure()
          ├── SwaggerToolLoader.LoadAsync(swaggerUrl)
          ├── DynamicToolRegistry.Initialize(tools, exposureService)
          └── next(app)   ← HTTP pipeline starts only after this completes
```

If `swagger.json` is unreachable (e.g. the upstream API is down), the filter logs
an error but does not crash the server — it starts with zero tools rather than failing.

---

### 6. `DynamicMcpToolHandler`
The single class that handles all MCP protocol interactions. Registered via the SDK's
custom handler hooks — no `[McpServerToolType]` attributes anywhere.

**`tools/list` request:**
Reads `DynamicToolRegistry.All` and maps each descriptor to an MCP `Tool` object
with the name, description, and JSON Schema the LLM client needs to call it correctly.

**`tools/call` request:**
Runs the full governance pipeline before touching the API:
1. Check tool exists and is enabled in exposure manifest
2. Check tool risk does not exceed global ceiling
3. Check tenant is permitted (if `allowedTenants` is set)
4. Delegate to `SwaggerToolInvoker`
5. Return result as `TextContentBlock`

---

### 7. `SwaggerToolInvoker`
Translates a tool call (name + arguments dictionary) into a real HTTP request and
returns the response as a string.

**Path parameter substitution:**
```
Template:   /api/orders/{id}
Arguments:  { "id": "42" }
Result:     /api/orders/42
```

**Query string assembly:**
```
Template:   /api/orders
Arguments:  { "status": "active", "page": "1" }
Result:     /api/orders?status=active&page=1
```

**Body serialization:**
For `POST`, `PUT`, `PATCH` — if the LLM passes a `"body"` key in arguments,
its value is serialized as the `application/json` request body.

**Correlation ID forwarding:**
Every outbound request carries `X-Correlation-Id` from the MCP request context,
enabling end-to-end tracing across the MCP server and your WebAPI.

---

### 8. `mcp_exposure.json`
The operator control plane. This file is the **only thing you edit** when onboarding
or restricting API endpoints.

```json
{
  "version": 1,
  "globalEnabled": true,
  "globalMaxRisk": "Medium",
  "tools": {
    "get_api_orders": {
      "enabled": true,
      "risk": "ReadOnly",
      "allowedTenants": []
    },
    "post_api_orders": {
      "enabled": true,
      "risk": "Low",
      "allowedTenants": ["tenant-a", "tenant-b"]
    },
    "delete_api_orders_id": {
      "enabled": false,
      "risk": "High",
      "allowedTenants": []
    }
  }
}
```

**Risk levels** (lowest to highest): `ReadOnly` → `Low` → `Medium` → `High`

A tool call is blocked if its declared `risk` exceeds `globalMaxRisk`.
`delete_api_orders_id` above is doubly blocked: `enabled: false` AND `risk: High`
exceeds the global ceiling of `Medium`.

---

## Onboarding a New Endpoint — Zero Code Required

Say your WebAPI adds `GET /api/invoices/{id}` with `operationId: GetInvoiceById`.

**Step 1 — Add one entry to `mcp_exposure.json`:**
```json
"get_invoice_by_id": {
  "enabled": true,
  "risk": "ReadOnly",
  "allowedTenants": []
}
```

**Step 2 — Restart the MCP server.**

That's it. The tool appears in `tools/list`, the LLM can call it, and it flows through
the full governance pipeline automatically.

---

## What Gets Logged at Startup

```
info  SwaggerToolLoader     Fetching OpenAPI spec from https://localhost:7001/swagger/v1/swagger.json
info  SwaggerToolLoader     Loaded 12 tools from OpenAPI spec.
debug DynamicToolRegistry   Tool post_api_orders_id skipped — not in exposure manifest or disabled.
debug DynamicToolRegistry   Tool delete_api_orders_id skipped — not in exposure manifest or disabled.
info  DynamicToolRegistry   Tool get_api_orders activated: GET /api/orders
info  DynamicToolRegistry   Tool get_api_orders_id activated: GET /api/orders/{id}
info  DynamicToolRegistry   Tool post_api_orders activated: POST /api/orders
info  DynamicToolRegistry   3 tools active after exposure filtering.
```

---

## Request Flow — End to End

```
LLM Client
  │
  │  POST /api/mcp
  │  { "method": "tools/call", "params": { "name": "get_api_orders_id", "arguments": { "id": "99" } } }
  │
  ▼
MCP SDK (WithCallToolHandler)
  │
  ▼
DynamicMcpToolHandler.CallToolAsync()
  ├── ExposureService.TryGetEnabledPolicy("get_api_orders_id") → ✓ enabled
  ├── PolicyEngine.EnforceToolPolicy() → risk ReadOnly ≤ Medium ✓, tenant ✓
  ├── DynamicToolRegistry.TryGet("get_api_orders_id") → descriptor found
  │
  ▼
SwaggerToolInvoker.InvokeAsync()
  ├── BuildUrl()  →  /api/orders/99
  ├── BuildRequest()  →  GET /api/orders/99  +  X-Correlation-Id: abc-123
  │
  ▼
Your WebAPI  →  GET /api/orders/99
  │
  ▼  { "id": 99, "status": "shipped", ... }
  │
SwaggerToolInvoker  →  returns JSON string
  │
DynamicMcpToolHandler  →  CallToolResult { Content: [ TextContentBlock("{"id":99,...}") ] }
  │
MCP SDK  →  JSON-RPC response to LLM client
```