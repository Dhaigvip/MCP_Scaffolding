# Mcp.Server.Http

The runnable host for the **standalone MCP server**. A thin `Program.cs` that wires
together all the library projects into an ASP.NET Core web application.

Use this project when the MCP server runs as its **own process** (Scenario 1 — Swagger,
or Scenario 3 — Palma remote). For the in-process scenario (Scenario 2), the Palma web
API is the host and this project is not used.

---

## What `Program.cs` does

```csharp
builder.Services
    .AddSwaggerMcp(config)           // Tool loader + invoker for external Swagger API
    .AddGovernance(config)           // mcp_exposure.json, risk engine
    .AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>()
    .AddMcpServerHandlers()          // MCP SDK: tools/list + tools/call handlers
    .AddAnthropic(config)            // Claude models
    .AddOpenAi(config)               // GPT models
    .AddGemini(config)               // Gemini models
    .AddAgent()                      // AgentService, SessionManager
    .AddAgentCors()
    .AddControllers();               // Picks up Mcp.Http controllers

app.UseAgentCors();
app.UseWebSockets();                 // Before MapControllers — needed for WS upgrade
app.MapMcp("/api/mcp");              // MCP JSON-RPC endpoint
app.MapControllers();                // Agent API (session, ws, chat, approve)
```

The project itself contains **no business logic** — all behaviour lives in the library
projects it references.

---

## Switching scenarios

### Scenario 1 — External Swagger API (default)

Use the existing `Program.cs` as-is. Configure `appsettings.json`:

```json
{
  "SwaggerMcp": {
    "SwaggerUrl": "https://your-api.example.com/swagger/v1/swagger.json",
    "ApiBaseUrl": "https://your-api.example.com"
  }
}
```

### Scenario 3 — Palma remote (separate process, OAuth2)

Replace `AddSwaggerMcp` with `AddPalmaMcpRemote`:

```csharp
builder.Services
    .AddPalmaMcpRemote(builder.Configuration)   // ← changed
    .AddGovernance(builder.Configuration)
    // ... rest unchanged
```

Configure `appsettings.json`:

```json
{
  "PalmaMcpRemote": {
    "ApiBaseUrl":         "https://palma.example.com",
    "SchemeId":           "PalmaScheme",
    "TokenEndpoint":      "https://identity.example.com/connect/token",
    "ClientId":           "mcp-server",
    "ClientSecret":       "SECRET",
    "Scope":              "palma-api",
    "EndpointsPath":      "/api/mcp/endpoints",
    "ContextQueryParams": ["org", "ms", "branch"]
  }
}
```

---

## Configuration

Full `appsettings.json` example:

```json
{
  "SwaggerMcp": {
    "SwaggerUrl": "https://api.example.com/swagger/v1/swagger.json",
    "ApiBaseUrl": "https://api.example.com"
  },
  "Governance": {
    "ExposureManifestPath": "mcp_exposure.json"
  },
  "Anthropic": { "ApiKey": "sk-ant-..." },
  "OpenAI":    { "ApiKey": "sk-..." },
  "Gemini":    { "ApiKey": "AI..." },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

**Secrets** (do not commit to source control):

```bash
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "OpenAI:ApiKey"    "sk-..."
dotnet user-secrets set "Gemini:ApiKey"    "AI..."
```

---

## mcp_exposure.json

Place this file in the project root (copied to output). Controls which tools are
enabled and at what risk level:

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
      "allowedTenants": ["acme"]
    }
  }
}
```

See `Mcp.Governance/README.md` for full governance documentation.

---

## Running

```bash
cd Mcp.Server.Http
dotnet run
```

Default URLs: `http://localhost:5000` / `https://localhost:5001`.

**Endpoints available after startup:**

| Path | Description |
|---|---|
| `POST /api/mcp` | MCP JSON-RPC (tools/list, tools/call) |
| `POST /api/agent/session` | Create chat session |
| `DELETE /api/agent/session/{id}` | Delete session |
| `GET /api/agent/ws/{id}` | WebSocket (preferred) |
| `POST /api/agent/chat` | SSE stream |
| `POST /api/agent/approve` | HITL approval (SSE) |

---

## Project dependencies

```
Mcp.Server.Http  (executable)
  ├── Mcp.Agent
  ├── Mcp.Governance
  ├── Mcp.Http
  └── Mcp.Server
```
