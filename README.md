# Palma MCP — Model Context Protocol Server

A production-ready MCP server that exposes REST API endpoints as tools for LLM agents.
Supports three deployment scenarios, a multi-model agent loop with HITL approval, and a
governance layer that controls which tools are visible at what risk level.

---

## Table of Contents

- [Solution Layout](#solution-layout)
- [How It Works — The Big Picture](#how-it-works--the-big-picture)
- [Scenario 1 — Swagger (External API)](#scenario-1--swagger-external-api)
- [Scenario 2 — Palma In-Process (Co-hosted)](#scenario-2--palma-in-process-co-hosted)
- [Scenario 3 — Palma Remote (Separate Process)](#scenario-3--palma-remote-separate-process)
- [Governance — mcp_exposure.json](#governance--mcp_exposurejson)
- [Agent Loop & HITL](#agent-loop--hitl)
- [HTTP API Reference](#http-api-reference)
- [React Client Integration](#react-client-integration)
- [Configuration Reference](#configuration-reference)

---

## Solution Layout

```
McpServer.sln
├── Mcp.Palma.Contracts/   Zero-dependency shared contracts (DTOs + interfaces)
│                          Referenced by Palma web API AND Mcp.Server
├── Mcp.Governance/        Exposure manifest + policy engine (risk & tenant gates)
├── Mcp.Server/            Core MCP pipeline: loaders, invokers, registry, handlers
├── Mcp.Agent/             LLM agent loop (multi-model), session management, HITL
├── Mcp.Http/              ASP.NET controllers (Agent WebSocket/SSE + MCP admin)
├── Mcp.Server.Http/       Runnable host — thin Program.cs wires everything together
└── Mcp.Client/            React 19 + TypeScript frontend (AgentChat component)
```

### Dependency graph

```
Mcp.Palma.Contracts   (no deps)
       ↑
Mcp.Governance        (ASP.NET Core only)
       ↑
Mcp.Server            (Governance + Palma.Contracts + MCP SDK)
       ↑
Mcp.Agent             (Governance + Mcp.Server)
       ↑
Mcp.Http              (Mcp.Agent + Mcp.Server)
       ↑
Mcp.Server.Http       (all of the above — runnable host)
```

---

## How It Works — The Big Picture

```
┌─────────────────────────────────────────────────────────────┐
│                      STARTUP                                │
│                                                             │
│  IToolLoader.LoadAsync()                                    │
│    ├── SwaggerToolLoader   → fetches swagger.json           │
│    ├── PalmaToolLoader     → reads IPalmaEndpointSource     │
│    └── PalmaRemoteToolLoader → GET /api/mcp/endpoints       │
│                  ↓                                          │
│         DynamicToolRegistry  (filtered by mcp_exposure.json)│
└─────────────────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────────────┐
│                     RUNTIME                                 │
│                                                             │
│  MCP Client → tools/list  → DynamicMcpToolHandler          │
│             → tools/call  → GovernanceExecutor              │
│                               ↓                            │
│                          IToolInvoker.InvokeAsync()         │
│                               ↓                            │
│                         Your API (HTTP or in-process)       │
└─────────────────────────────────────────────────────────────┘
```

Tool loading and invocation are separated by two interfaces:

| Interface | Role |
|---|---|
| `IToolLoader` | Discovers tools at startup → returns `List<SwaggerToolDescriptor>` |
| `IToolInvoker` | Executes a tool call at runtime → returns JSON string |

Three concrete implementations cover all three scenarios.

---

## Scenario 1 — Swagger (External API)

**Use when:** Your API exposes a standard OpenAPI / Swagger spec and the MCP server
runs as a standalone process.

```
MCP Server (Mcp.Server.Http)
     │  startup: GET /swagger/v1/swagger.json
     │──────────────────────────────────────▶ Your WebAPI
     │
     │  runtime: HTTP calls per tool invocation
     │──────────────────────────────────────▶ Your WebAPI
```

### Setup

**1. `appsettings.json`**

```json
{
  "SwaggerMcp": {
    "SwaggerUrl": "https://your-api.example.com/swagger/v1/swagger.json",
    "ApiBaseUrl": "https://your-api.example.com"
  }
}
```

**2. `Program.cs`** (already configured in `Mcp.Server.Http`)

```csharp
builder.Services
    .AddSwaggerMcp(builder.Configuration)   // IToolLoader + IToolInvoker for Swagger
    .AddGovernance(builder.Configuration)
    .AddMcpServerHandlers()
    .AddAgent()
    // ...
```

**3. `mcp_exposure.json`** — allow-list the operations you want exposed

```json
{
  "version": 1,
  "globalEnabled": true,
  "globalMaxRisk": "Medium",
  "tools": {
    "get_api_orders": { "enabled": true, "risk": "ReadOnly", "allowedTenants": [] },
    "post_api_orders": { "enabled": true, "risk": "Low",      "allowedTenants": [] }
  }
}
```

**4. Restart the MCP server.** Tools are auto-registered at startup.

### How tool names are derived

| Source | Example operation | Tool name |
|---|---|---|
| `operationId` | `GetOrderById` | `get_order_by_id` |
| method + path | `GET /api/orders/{id}` | `get_api_orders_id` |

---

## Scenario 2 — Palma In-Process (Co-hosted)

**Use when:** The MCP server runs **inside** the same process as the Palma web API.
No loopback HTTP — tool invocations are direct in-process calls.

```
Palma Web API process
  ├── Palma controllers        (SemVerHandlers etc.)
  ├── IPalmaEndpointSource     ← implemented by Palma, used by PalmaToolLoader
  ├── IPalmaToolInvoker        ← implemented by Palma, used by PalmaToolInvoker
  └── MCP pipeline (Mcp.Server, Mcp.Agent, Mcp.Http all referenced in-process)
```

### Project references required in Palma web API

```xml
<!-- Palma.WebApi.csproj -->
<ProjectReference Include="..\Palma_MCP\Mcp.Server\Mcp.Server.csproj" />
<ProjectReference Include="..\Palma_MCP\Mcp.Agent\Mcp.Agent.csproj" />
<ProjectReference Include="..\Palma_MCP\Mcp.Http\Mcp.Http.csproj" />
<ProjectReference Include="..\Palma_MCP\Mcp.Governance\Mcp.Governance.csproj" />
<ProjectReference Include="..\Palma_MCP\Mcp.Palma.Contracts\Mcp.Palma.Contracts.csproj" />
```

### Implement the two contracts

```csharp
// 1. Endpoint discovery — tells MCP which operations exist
public class PalmaEndpointSourceImpl : IPalmaEndpointSource
{
    public IEnumerable<PalmaEndpointInfo> GetEndpoints()
    {
        // Return your SemVerHandlers registrations as PalmaEndpointInfo records
        yield return new PalmaEndpointInfo
        {
            Uri         = "GET/modules",
            Summary     = "List all modules",
            QueryParams =
            [
                new PalmaQueryParamInfo { Name = "org",    Required = true,  Type = "string" },
                new PalmaQueryParamInfo { Name = "ms",     Required = false, Type = "string" },
                new PalmaQueryParamInfo { Name = "branch", Required = false, Type = "string" }
            ]
        };
    }
}

// 2. In-process invocation — MCP calls this instead of HTTP
public class PalmaToolInvokerImpl : IPalmaToolInvoker
{
    private readonly SemVerHandlers _handlers;

    public async Task<string> InvokeAsync(
        string uri,
        PalmaContext context,
        IReadOnlyDictionary<string, string> additionalParams,
        CancellationToken ct)
    {
        // Call your SemVerHandlers directly
        var result = await _handlers.HandleAsync(uri, context, additionalParams, ct);
        return JsonSerializer.Serialize(result);
    }
}
```

### Register everything in Palma's `Program.cs`

```csharp
// Palma's own services
builder.Services.AddSingleton<SemVerHandlers>();
builder.Services.AddSingleton<IPalmaEndpointSource, PalmaEndpointSourceImpl>();
builder.Services.AddSingleton<IPalmaToolInvoker, PalmaToolInvokerImpl>();

// MCP pipeline (must come AFTER the two above)
builder.Services
    .AddPalmaMcp(builder.Configuration)     // IToolLoader + IToolInvoker (in-process)
    .AddGovernance(builder.Configuration)
    .AddMcpServerHandlers()
    .AddAgent()
    .AddAgentCors()
    .AddControllers();

// ...
app.UseWebSockets();
app.MapMcp("/api/mcp");
app.MapControllers();
```

**`appsettings.json`** for in-process scenario:

```json
{
  "PalmaMcp": {
    "ContextQueryParams": ["org", "ms", "branch"]
  }
}
```

`ContextQueryParams` are stripped from the tool schema — the LLM never sees them.
They are injected automatically from `PalmaContext` when a tool is called.

### Client session creation

When starting a chat session from the React client, pass the Palma context:

```typescript
// POST /api/agent/session
{
  "palmaContext": {
    "version": "1",
    "org": "acme-corp",
    "ms": "erp",
    "branch": "main"
  }
}
```

---

## Scenario 3 — Palma Remote (Separate Process)

**Use when:** The MCP server and the Palma web API run as **separate processes**
(different repositories, different deployments). Communication is over HTTP with
OAuth2 client credentials authentication.

```
MCP Server (Mcp.Server.Http)                 Palma Web API
     │                                              │
     │  startup: GET /api/mcp/endpoints             │
     │  Authorization: Bearer {token}               │
     │  AuthScheme: {SchemeId}                      │
     │─────────────────────────────────────────────▶│
     │                                              │
     │  runtime: POST /API/v{ver}/{uri}?org=...     │
     │  Authorization: Bearer {token}               │
     │  AuthScheme: {SchemeId}                      │
     │─────────────────────────────────────────────▶│
```

### What the Palma web API must expose

**Endpoint 1 — Tool discovery** (`GET /api/mcp/endpoints`)

```csharp
// McpController.cs in Palma web API
[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IPalmaEndpointSource _source;

    [HttpGet("endpoints")]
    [Authorize]   // honour the Bearer token + AuthScheme headers
    public IActionResult GetEndpoints()
        => Ok(_source.GetEndpoints());
}
```

Palma web API only needs `Mcp.Palma.Contracts` — no other MCP project reference:

```xml
<!-- Palma.WebApi.csproj (Scenario 3 — remote only) -->
<ProjectReference Include="..\Palma_MCP\Mcp.Palma.Contracts\Mcp.Palma.Contracts.csproj" />
```

**Endpoint 2 — Tool invocation** — reuses the **existing** Palma route:

```
POST /API/v{version}/{*uri}?org={org}&ms={ms}&branch={branch}&{llm-params}
Authorization: Bearer {token}
AuthScheme: {SchemeId}
```

No new controller needed. `PalmaRemoteToolInvoker` constructs this URL from
`PalmaContext` + the LLM-supplied arguments.

### MCP server configuration

**`appsettings.json`**

```json
{
  "PalmaMcpRemote": {
    "ApiBaseUrl":          "https://palma.example.com",
    "SchemeId":            "PalmaScheme",
    "TokenEndpoint":       "https://identity.example.com/connect/token",
    "ClientId":            "mcp-server",
    "ClientSecret":        "your-secret",
    "Scope":               "palma-api",
    "EndpointsPath":       "/api/mcp/endpoints",
    "ContextQueryParams":  ["org", "ms", "branch"]
  }
}
```

**`Program.cs`** in `Mcp.Server.Http`

```csharp
builder.Services
    .AddPalmaMcpRemote(builder.Configuration)  // IToolLoader + IToolInvoker (remote)
    .AddGovernance(builder.Configuration)
    .AddMcpServerHandlers()
    .AddAgent()
    .AddAgentCors()
    .AddControllers();
```

### Authentication flow

1. `PalmaTokenProvider` (singleton) calls `POST {TokenEndpoint}` with
   `client_credentials` grant at first request and caches the token.
2. Token is re-fetched 60 seconds before expiry (double-checked via `SemaphoreSlim`).
3. Every request carries `Authorization: Bearer {token}` and `AuthScheme: {SchemeId}`.

---

## Governance — mcp_exposure.json

All scenarios share the same governance layer. Edit `mcp_exposure.json` to control
which tools are visible and at what risk.

```json
{
  "version": 1,
  "globalEnabled": true,
  "globalMaxRisk": "Medium",
  "tools": {
    "get_modules": {
      "enabled": true,
      "risk": "ReadOnly",
      "allowedTenants": []
    },
    "post_api_orders": {
      "enabled": true,
      "risk": "Low",
      "allowedTenants": ["acme", "globex"]
    },
    "delete_api_data": {
      "enabled": false,
      "risk": "High",
      "allowedTenants": []
    }
  }
}
```

**Risk levels** (lowest → highest): `ReadOnly` → `Low` → `Medium` → `High`

A tool call is blocked if its `risk` exceeds `globalMaxRisk`.
`delete_api_data` is doubly blocked: `enabled: false` AND `High > Medium`.

**To onboard a new endpoint:** add one entry to `mcp_exposure.json` and restart. No code changes.

---

## Agent Loop & HITL

`AgentService` runs a multi-turn agentic loop powered by any supported LLM:

| Provider | Config key | Model selection |
|---|---|---|
| Anthropic (Claude) | `Anthropic:ApiKey` | Default or per-session |
| OpenAI (GPT) | `OpenAI:ApiKey` | Default or per-session |
| Google (Gemini) | `Gemini:ApiKey` | Default or per-session |

### Human-in-the-Loop (HITL)

Tools with risk ≥ the HITL threshold pause the agent loop and emit a `hitl` event.
The frontend displays a confirmation dialog. The user approves or rejects.

**WebSocket flow:**

```
Agent emits  → { type: "hitl",    callId, toolName, input, risk }
User clicks  → { type: "approve", callId, approved: true }
Agent resumes automatically
```

**SSE flow:**

```
Agent emits   → data: { type: "hitl", callId, ... }
User confirms → POST /api/agent/approve  { sessionId, callId, approved: true }
Agent resumes (SSE stream stays open during the wait)
```

---

## HTTP API Reference

### Session management

```
POST   /api/agent/session         Create a session → { sessionId }
DELETE /api/agent/session/{id}    Delete a session
```

**Create session body (Palma scenarios):**
```json
{
  "palmaContext": {
    "version": "1",
    "org":     "acme-corp",
    "ms":      "erp",
    "branch":  "main"
  }
}
```
Omit body entirely for the Swagger scenario.

### Chat transports

```
GET  /api/agent/ws/{sessionId}    WebSocket — bidirectional, preferred
POST /api/agent/chat              SSE stream; body: { sessionId, message }
POST /api/agent/approve           Resolve HITL (SSE only); body: { sessionId, callId, approved }
```

### MCP protocol endpoint

```
POST /api/mcp                     MCP JSON-RPC (tools/list, tools/call)
```

### Agent event types (server → client)

| `type` | Payload fields | Description |
|---|---|---|
| `thinking` | `thinking` | Extended thinking block |
| `status` | `message` | Status update |
| `text_delta` | `delta` | Streaming text chunk |
| `text_end` | — | Text turn complete |
| `tool_auto` | `toolName`, `input` | Tool called automatically |
| `hitl` | `callId`, `toolName`, `input`, `risk` | Awaiting user approval |
| `tool_skipped` | `toolName` | User rejected tool call |
| `tool_result` | `toolName`, `result`, `isError` | Tool execution result |
| `done` | — | Turn complete |
| `error` | `message` | Error occurred |

---

## React Client Integration

The `Mcp.Client` project is a React 19 + TypeScript app using the `AgentChat`
component.

### Usage

```tsx
import { AgentChat } from './components/AgentChat';

// Swagger scenario
<AgentChat baseUrl="https://mcp.example.com" />

// Palma scenario (passes context to session creation)
<AgentChat
  baseUrl="https://mcp.example.com"
  palmaContext={{ version: "1", org: "acme", ms: "erp", branch: "main" }}
/>
```

### Transport selection

The component auto-negotiates: WebSocket is preferred (lower latency, no
reconnect for HITL). SSE is the fallback. The user can toggle manually.

### HITL dialog

When the agent emits a `hitl` event, the `HitlDialog` component appears with
the tool name, input arguments, and risk level. The user clicks Approve or Reject.
The dialog is non-blocking — the SSE stream stays alive during the wait.

---

## Configuration Reference

### `appsettings.json` — full example

```json
{
  "SwaggerMcp": {
    "SwaggerUrl": "https://api.example.com/swagger/v1/swagger.json",
    "ApiBaseUrl": "https://api.example.com"
  },

  "PalmaMcp": {
    "ContextQueryParams": ["org", "ms", "branch"]
  },

  "PalmaMcpRemote": {
    "ApiBaseUrl":         "https://palma.example.com",
    "SchemeId":           "PalmaScheme",
    "TokenEndpoint":      "https://identity.example.com/connect/token",
    "ClientId":           "mcp-server",
    "ClientSecret":       "SECRET",
    "Scope":              "palma-api",
    "EndpointsPath":      "/api/mcp/endpoints",
    "ContextQueryParams": ["org", "ms", "branch"]
  },

  "Governance": {
    "ExposureManifestPath": "mcp_exposure.json"
  },

  "Anthropic": {
    "ApiKey": "sk-ant-..."
  },

  "OpenAI": {
    "ApiKey": "sk-..."
  },

  "Gemini": {
    "ApiKey": "AI..."
  },

  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

> **Tip:** Store secrets in User Secrets during development:
> `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."`

### Which `Add*Mcp` method to call

| Scenario | Registration method | Required config section |
|---|---|---|
| External Swagger API | `AddSwaggerMcp()` | `SwaggerMcp` |
| Palma in-process | `AddPalmaMcp()` | `PalmaMcp` |
| Palma separate process | `AddPalmaMcpRemote()` | `PalmaMcpRemote` |

All three call `AddMcpServerHandlers()` to wire up the MCP SDK handlers.
