# Mcp.Http

ASP.NET Core class library containing the HTTP controllers and CORS configuration
for the agent API. Extracted from the host project so it can be referenced by any
host — including the Palma web API in the in-process scenario.

**Class library with `FrameworkReference: Microsoft.AspNetCore.App`.**
Not an executable — it is wired into the host via `AddControllers()` + `MapControllers()`.

---

## Controllers

### `AgentController`  (`/api/agent`)

The full HTTP surface consumed by the React frontend.

#### Session management

```
POST   /api/agent/session
DELETE /api/agent/session/{id}
```

**POST body** (Palma scenarios — omit for Swagger):

```json
{
  "palmaContext": {
    "version": "1",
    "org": "acme-corp",
    "ms": "erp",
    "branch": "main"
  }
}
```

**POST response:**

```json
{ "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
```

---

#### WebSocket — `GET /api/agent/ws/{sessionId}`

Full-duplex transport. Preferred over SSE.

**Client → Server frames (JSON):**

```json
{ "type": "chat",    "message": "List all modules for org acme" }
{ "type": "approve", "callId": "call_abc123", "approved": true }
```

**Server → Client frames (JSON):** same `AgentEvent` stream as SSE (see below).

**Implementation notes:**
- A single WebSocket connection supports many turns — no reconnect needed.
- HITL approval travels on the same socket; no extra HTTP round-trip.
- An unbounded `Channel<AgentEvent>` serialises writes from concurrent agent turns,
  preventing `WebSocket` data-race exceptions.
- The send loop catches `OperationCanceledException` cleanly when the client
  disconnects or switches transport.

---

#### SSE — `POST /api/agent/chat`

One-way server stream. Use when WebSocket is unavailable.

**Request body:**

```json
{ "sessionId": "...", "message": "List all modules" }
```

**Response:** `Content-Type: text/event-stream`

```
data: {"type":"status","message":"Calling get_modules..."}

data: {"type":"text_delta","delta":"Here are the modules:"}

data: {"type":"hitl","callId":"call_abc","toolName":"post_module","input":{...},"risk":{...}}

data: {"type":"done"}
```

The SSE connection **stays open** while the agent is suspended waiting for HITL
approval. Resume by calling `POST /api/agent/approve` on a separate request.

---

#### HITL approval — `POST /api/agent/approve`

Used by SSE clients. WebSocket clients send an `approve` frame directly on the socket.

**Request body:**

```json
{ "sessionId": "...", "callId": "call_abc123", "approved": true }
```

**Response:**

```json
{ "resolved": true }
```

---

### `McpAdminController`  (`/api/mcp`)

Administrative endpoints for the MCP pipeline itself (tool listing, registry refresh,
governance reload). Not part of the agent chat flow.

---

## CORS

`Cors.cs` provides `AddAgentCors()` and `UseAgentCors()` extension methods.

**`appsettings.json`:**

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://your-frontend.example.com"
    ]
  }
}
```

**Registration:**

```csharp
// Program.cs
builder.Services.AddControllers();
builder.Services.AddAgentCors();

// ...
app.UseAgentCors();
app.UseWebSockets();   // must come before MapControllers
app.MapControllers();
```

---

## Agent event types reference

| `type` | Additional fields | Description |
|---|---|---|
| `thinking` | `thinking: string` | Extended thinking block from the model |
| `status` | `message: string` | Human-readable status update |
| `text_delta` | `delta: string` | Streaming text chunk (16 chars each) |
| `text_end` | — | Text stream for this assistant turn is complete |
| `tool_auto` | `toolName`, `input` | Tool executed automatically (low risk) |
| `hitl` | `callId`, `toolName`, `input`, `risk` | Paused — awaiting user approval |
| `tool_skipped` | `toolName` | User rejected the tool call |
| `tool_result` | `toolName`, `result`, `isError` | Tool execution result |
| `done` | — | Full agent turn complete |
| `error` | `message: string` | Unrecoverable error |

---

## Project dependencies

```
Mcp.Http
  ├── Mcp.Agent    (AgentService, SessionManager, AgentEvent)
  └── Mcp.Server   (PalmaContext — via Mcp.Palma.Contracts)
```
