# Mcp.Agent

Multi-model LLM agent loop with session management and Human-in-the-Loop (HITL) support.

**Class library** — consumed by `Mcp.Http` (controllers) and `Mcp.Server.Http` (host).

---

## Architecture

```
AgentController (Mcp.Http)
        │
        ▼
  AgentService.RunTurnAsync()          ← IAsyncEnumerable<AgentEvent>
        │
        ├── SessionManager             ← session lookup + history
        │
        ├── IAgentModelRouter          ← selects LLM per session
        │       ├── AnthropicAgentModel
        │       ├── OpenAiAgentModel
        │       └── GeminiAgentModel
        │
        ├── DynamicToolRegistry        ← tool descriptors (from Mcp.Server)
        │
        ├── IToolInvoker               ← executes tool call (from Mcp.Server)
        │
        └── IExposureService           ← determines HITL threshold (from Mcp.Governance)
```

---

## `AgentService`

The core agent loop. Provider-neutral — it works with any `IAgentModel` implementation.

### `RunTurnAsync(sessionId, userMessage, ct)`

Returns `IAsyncEnumerable<AgentEvent>` — the controller streams each event directly
to the client as it arrives (SSE or WebSocket).

**Loop steps:**

1. Add user message to session history.
2. Build neutral tool list from `DynamicToolRegistry`.
3. Call `IAgentModel.GenerateAsync()` → `AgentModelResponse`.
4. Stream thinking blocks and text chunks to the caller.
5. For each tool call in the response:
   - Check risk via `IExposureService`.
   - If HITL required: emit `HitlAgentEvent`, await `session.WaitForApproval()`.
   - If approved (or auto): call `IToolInvoker.InvokeAsync()`.
   - Emit `ToolResultAgentEvent`.
   - Add tool result to history.
6. Feed tool results back to the LLM and repeat from step 3.
7. When no tool calls remain, emit `DoneAgentEvent`.

### System prompt

The agent is instructed to answer exclusively through tool calls — never from
training knowledge. If no tool matches the user's request it explains what tools
are available. This makes the agent a faithful proxy for the underlying API.

---

## Session management

### `AgentSession`

Holds all per-session state:

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Unique session identifier (GUID) |
| `History` | `List<AgentMessage>` | Full conversation history |
| `ModelName` | `string?` | Override LLM model for this session |
| `PalmaContext` | `PalmaContext?` | Palma routing context (Scenarios 2 & 3) |

**HITL methods:**

```csharp
// Called by the agent loop — suspends the IAsyncEnumerable until resolved
Task<bool> WaitForApproval(string callId, CancellationToken ct)

// Called by the controller when the user clicks Approve/Reject
bool ResolveHitl(string callId, bool approved)
```

Internally uses a `ConcurrentDictionary<string, TaskCompletionSource<bool>>`.

### `SessionManager`

Singleton. Thread-safe dictionary of active sessions.

```csharp
AgentSession Create(PalmaContext? palmaContext = null)
AgentSession? Get(string sessionId)
void Delete(string sessionId)
```

Sessions are in-memory only. They are lost on server restart.

---

## Multi-model support

### `IAgentModel`

```csharp
public interface IAgentModel
{
    Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken ct);
}
```

### `IAgentModelRouter`

```csharp
public interface IAgentModelRouter
{
    IAgentModel Resolve(AgentSession session, AgentModelRequest request);
}
```

`AgentModelRouter` selects the model based on `session.ModelName`:
- `claude-*` → `AnthropicAgentModel`
- `gpt-*` / `o1-*` → `OpenAiAgentModel`
- `gemini-*` → `GeminiAgentModel`
- `null` / unrecognised → default Anthropic (Claude)

### Supported models

| Provider | Config | Default model |
|---|---|---|
| Anthropic | `Anthropic:ApiKey` | `claude-opus-4-5` with extended thinking |
| OpenAI | `OpenAI:ApiKey` | `gpt-4o` |
| Gemini | `Gemini:ApiKey` | `gemini-2.5-pro` |

---

## `AgentEvent` union

All events share a `type` discriminator (camelCase in JSON):

```csharp
// Emitted events (server → client)
ThinkingAgentEvent(string Thinking)
StatusAgentEvent(string Message)
TextDeltaAgentEvent(string Delta)
TextEndAgentEvent()
ToolAutoAgentEvent(string ToolName, IReadOnlyDictionary<string, JsonElement> Input)
HitlAgentEvent(string CallId, string ToolName, IReadOnlyDictionary<string, JsonElement> Input, RiskInfo Risk)
ToolSkippedAgentEvent(string ToolName)
ToolResultAgentEvent(string ToolName, string Result, bool IsError)
DoneAgentEvent()
ErrorAgentEvent(string Message)
```

---

## `AgentMessage` / `AgentMessagePart`

Neutral representation of conversation history — no provider-specific types.

```csharp
public class AgentMessage
{
    public AgentRole Role  { get; set; }   // User | Assistant
    public List<AgentMessagePart> Parts { get; set; }
}

// Part types: Text, Thinking, ToolCall, ToolResult
```

Each model adapter converts `List<AgentMessage>` to the provider's native format
and converts the provider's response back to `AgentModelResponse`.

---

## Registration

```csharp
// appsettings.json keys used:
//   Anthropic:ApiKey
//   OpenAI:ApiKey
//   Gemini:ApiKey

services
    .AddAnthropic(config)
    .AddOpenAi(config)
    .AddGemini(config)
    .AddAgent();
```

`AddAgent()` registers:
- `SessionManager` (singleton)
- `AgentModelRouter` (singleton)
- `AgentService` (singleton)

---

## Project dependencies

```
Mcp.Agent
  ├── Mcp.Governance    (IExposureService, RiskLevel, RiskInfo)
  └── Mcp.Server        (DynamicToolRegistry, IToolInvoker, AgentToolDescriptor)
```
