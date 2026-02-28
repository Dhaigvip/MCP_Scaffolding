# Mcp.Client

React 19 + TypeScript frontend for the AI agent chat interface.

**Stack:** React 19.2 · TypeScript 5.9 (strict) · Vite 7.3 · React Compiler (auto-memoisation)

No UI libraries. No path aliases — all imports are relative.

---

## Quick start

```bash
cd Mcp.Client
npm install
npm run dev          # http://localhost:5173
```

The server URL defaults to same-origin. For local development with a separate
.NET backend, set the proxy in `vite.config.ts` or pass `apiBase` as a prop.

---

## `AgentChat` component

The entire UI lives in `src/components/AgentChat/`. Import it as:

```tsx
import { AgentChat } from './components/AgentChat';
```

### Props

| Prop | Type | Default | Description |
|---|---|---|---|
| `apiBase` | `string` | `""` | Base URL of the .NET API, e.g. `"http://localhost:5000"` |
| `placeholder` | `string` | `"Ask the agent anything…"` | Textarea placeholder |
| `className` | `string` | `""` | Extra CSS class on the root element |
| `defaultProtocol` | `"ws" \| "sse"` | `"ws"` | Initial transport |
| `palmaContext` | `PalmaContext \| undefined` | — | Passed to session creation (Palma scenarios) |

### Basic usage (Swagger / standalone)

```tsx
<AgentChat apiBase="http://localhost:5000" />
```

### Palma in-process or remote

```tsx
<AgentChat
  apiBase="https://mcp.example.com"
  palmaContext={{
    version: "1",
    org:     "acme-corp",
    ms:      "erp",
    branch:  "main"
  }}
/>
```

The `palmaContext` is sent as the POST body when creating the session
(`POST /api/agent/session`). If omitted the body is empty, which is correct for
the Swagger scenario.

---

## Component architecture

```
AgentChat.tsx  (thin orchestrator — sendMessage lives here)
  │
  ├── hooks/useAgentSession.ts    POST/DELETE session lifecycle
  ├── hooks/useAgentWebSocket.ts  WebSocket lifecycle + message dispatch
  ├── hooks/useAgentSse.ts        SSE abort ref + abort-on-switch effect
  ├── hooks/useAgentMessages.ts   messages state + dispatchAgentEvent switch
  ├── hooks/useHitlResolution.ts  resolveHitl (send approve over WS or POST)
  │
  ├── components/MessageBubble.tsx    renders one chat message (user or assistant)
  ├── components/ToolCallCard.tsx     renders tool_auto / tool_result inline
  ├── components/HitlDialog.tsx       approve/reject dialog for high-risk tools
  │
  ├── types/events.ts     AgentEvent union (mirrors .NET AgentEvent hierarchy)
  ├── types/messages.ts   ChatMessage, HitlRecord, ToolCallRecord
  ├── types/index.ts      barrel — PalmaContext, Protocol, re-exports
  │
  └── utils/uid.ts, tryPrettyJson.ts
```

`sendMessage` stays in `AgentChat.tsx` because it wires every hook together —
extracting it would require passing the same set of arguments to a new hook.

---

## Transport protocols

### WebSocket (default — `protocol = "ws"`)

- Single connection per session; survives multiple chat turns without reconnect.
- HITL approval travels on the same socket (`{ type: "approve", callId, approved }`).
- `useAgentWebSocket` wraps the native `WebSocket` API. A `setTimeout` guard
  prevents Strict Mode's double-effect from opening two concurrent connections.

### SSE (`protocol = "sse"`)

- One `fetch` request per chat turn with `Accept: text/event-stream`.
- The connection **stays open** while the server is suspended waiting for HITL approval.
- HITL approval is sent via a separate `POST /api/agent/approve`.
- `useAgentSse` manages the `AbortController` ref; it aborts the current stream
  when the user switches to WebSocket.

The user can toggle between transports at any time via the header button.

---

## Message state (`useAgentMessages`)

`messages` is `ChatMessage[]`. Each element represents one bubble in the UI.

```typescript
interface ChatMessage {
  id:         string;
  role:       "user" | "assistant";
  text:       string;
  isStreaming?: boolean;
  toolCalls?: ToolCallRecord[];
  hitl?:      HitlRecord;
  error?:     string;
}
```

`dispatchAgentEvent(msgId, event)` is the single entry point for all server events.
A `switch` on `event.type` updates the message state:

| Event | Action |
|---|---|
| `text_delta` | Appends `delta` to `msg.text` |
| `text_end` | Clears `isStreaming` flag |
| `tool_auto` | Appends a `ToolCallRecord` with status `"running"` |
| `tool_result` | Updates the matching `ToolCallRecord` with the result |
| `hitl` | Sets `msg.hitl` — triggers `HitlDialog` to render |
| `tool_skipped` | Marks the HITL as rejected |
| `done` | Finalises the message |
| `error` | Sets `msg.error` |

---

## HITL dialog

When the server emits a `hitl` event (tool risk requires approval), `HitlDialog`
renders inside the assistant message bubble. It shows:

- Tool name and risk level (colour-coded: 🔴 High / 🟡 Medium / 🟢 Low / 🔵 ReadOnly)
- Tool input arguments (pretty-printed JSON)
- Approve and Reject buttons

`resolveHitl(callId, approved, msgId)` from `useHitlResolution`:
- **WebSocket:** sends `{ type: "approve", callId, approved }` on the socket.
- **SSE:** `POST /api/agent/approve` with `{ sessionId, callId, approved }`.

---

## AgentEvent types (TypeScript)

Mirrors the .NET `AgentEvent` class hierarchy:

```typescript
type AgentEvent =
  | ThinkingEvent     // { type: "thinking",     content: string }
  | StatusEvent       // { type: "status",       message: string }
  | TextDeltaEvent    // { type: "text_delta",   delta: string }
  | TextEndEvent      // { type: "text_end" }
  | HitlEvent         // { type: "hitl",         callId, toolName, input, risk }
  | ToolAutoEvent     // { type: "tool_auto",    toolName, input }
  | ToolResultEvent   // { type: "tool_result",  toolName, content, isError }
  | ToolSkippedEvent  // { type: "tool_skipped", toolName }
  | DoneEvent         // { type: "done" }
  | ErrorEvent        // { type: "error",        message: string }
```

---

## Styling

CSS is scoped to `.agent-chat` via custom properties — no global leakage:

```css
.agent-chat {
  --ac-primary:   #4f8ef7;
  --ac-success:   #22c55e;
  --ac-warning:   #f59e0b;
  --ac-error:     #ef4444;
  /* ... */
}
```

Override variables on a parent element to theme the component.

---

## React Compiler notes

The React Compiler (via `@vitejs/plugin-react` v5) is active. This means:

- **Do not add manual `useCallback` / `useMemo`** unless you have a specific reason.
  The compiler auto-memoises components and callbacks.
- `useCallback([])` is used only on `updateMsg` in `useAgentMessages` to give it
  a stable identity that hooks can safely depend on.
- `resolveHitl` is a **plain function** (no `useCallback`) — manual wrapping caused
  "Compilation Skipped" warnings due to `ref` dependencies.

---

## Build for production

```bash
npm run build    # outputs to dist/
npm run preview  # preview the production build locally
```

Configure the API base URL via a Vite env variable:

```
# .env.production
VITE_API_BASE=https://mcp.example.com
```

```tsx
<AgentChat apiBase={import.meta.env.VITE_API_BASE} />
```
