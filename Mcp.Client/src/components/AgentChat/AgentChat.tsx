/**
 * AgentChat.tsx — thin orchestrator
 *
 * Composes hooks and sub-components into the AI Agent chat UI.
 * Supports two streaming protocols, switchable via the header toggle:
 *   • WebSocket (ws/wss) — full-duplex; HITL approval on the same connection.
 *   • SSE (text/event-stream) — one-way stream; HITL approval via separate POST.
 *
 * Usage:
 *   import { AgentChat } from './AgentChat';
 *   <AgentChat apiBase="http://localhost:5000" />
 */

import { useCallback, useEffect, useRef, useState } from "react";
import "./AgentChat.css";

import type { AgentEvent, Protocol, PalmaContext } from "./types";
import { uid } from "./utils/uid";

import { useAgentMessages } from "./hooks/useAgentMessages";
import { useAgentSession } from "./hooks/useAgentSession";
import { useAgentWebSocket } from "./hooks/useAgentWebSocket";
import { useAgentSse } from "./hooks/useAgentSse";
import { useHitlResolution } from "./hooks/useHitlResolution";

import { MessageBubble } from "./components/MessageBubble";

// ─── Props ────────────────────────────────────────────────────────────────────

interface AgentChatProps {
    /** Base URL of the .NET API, e.g. "http://localhost:5000" (or "" for same-origin) */
    apiBase?: string;
    placeholder?: string;
    className?: string;
    /** Initial transport protocol — can be toggled at runtime in the header. */
    defaultProtocol?: Protocol;
    /**
     * Optional Palma context (version, org, ms, branch) sent to the server at
     * session creation and injected into every Palma tool call.  Omit when using
     * the Swagger pipeline — the context is not needed and will be ignored.
     */
    palmaContext?: PalmaContext;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function AgentChat({
    apiBase = "",
    placeholder = "Ask the agent anything…",
    className = "",
    defaultProtocol = "ws",
    palmaContext
}: AgentChatProps) {
    const [input, setInput] = useState("");
    const [protocol] = useState<Protocol>(defaultProtocol);
    const bottomRef = useRef<HTMLDivElement>(null);

    // ── Core message state + event dispatcher ─────────────────────────────────
    const {
        messages,
        setMessages,
        isStreaming,
        setIsStreaming,
        currentAsstMsgIdRef,
        pendingApprovalRef,
        updateMsg,
        dispatchAgentEvent
    } = useAgentMessages();

    // ── Session lifecycle ─────────────────────────────────────────────────────
    const { sessionId } = useAgentSession({ apiBase, setMessages, palmaContext });

    // ── WebSocket lifecycle ───────────────────────────────────────────────────
    const { wsRef, wsConnected } = useAgentWebSocket({
        apiBase,
        sessionId,
        protocol,
        currentAsstMsgIdRef,
        dispatchAgentEvent,
        setIsStreaming,
        updateMsg,
        setMessages
    });

    // ── SSE abort ref ─────────────────────────────────────────────────────────
    const { sseAbortRef } = useAgentSse({ protocol });

    // ── HITL approval ─────────────────────────────────────────────────────────
    const { resolveHitl } = useHitlResolution({
        protocol,
        sessionId,
        apiBase,
        wsRef,
        pendingApprovalRef,
        updateMsg
    });

    // ── Auto-scroll ───────────────────────────────────────────────────────────
    useEffect(() => {
        bottomRef.current?.scrollIntoView({ behavior: "smooth" });
    }, [messages]);

    // ── Send message ──────────────────────────────────────────────────────────
    // Lives here because it wires every hook together; extracting it would
    // require passing the same number of arguments to a new hook.
    const sendMessage = useCallback(async (): Promise<void> => {
        if (!input.trim() || isStreaming) return;
        if (protocol === "ws" && (!wsRef.current || wsRef.current.readyState !== WebSocket.OPEN)) return;
        if (protocol === "sse" && !sessionId) return;

        const userText = input.trim();
        setInput("");
        setIsStreaming(true);

        // User bubble
        setMessages((prev) => [...prev, { id: uid(), role: "user", text: userText }]);

        // Assistant placeholder bubble — ID tracked so events land in the right bubble
        const asstMsgId = uid();
        currentAsstMsgIdRef.current = asstMsgId;
        setMessages((prev) => [
            ...prev,
            { id: asstMsgId, role: "assistant", text: "", isStreaming: true, toolCalls: [] }
        ]);

        if (protocol === "ws") {
            // WebSocket path — responses arrive asynchronously via ws.onmessage
            wsRef.current!.send(JSON.stringify({ type: "chat", message: userText }));
        } else {
            // SSE path
            sseAbortRef.current = new AbortController();
            try {
                const resp = await fetch(`${apiBase}/api/agent/chat`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", Accept: "text/event-stream" },
                    body: JSON.stringify({ sessionId, message: userText }),
                    signal: sseAbortRef.current.signal
                });
                if (!resp.ok) throw new Error(`HTTP ${resp.status}`);

                const reader = resp.body!.getReader();
                const decoder = new TextDecoder();
                let buffer = "";

                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    buffer += decoder.decode(value, { stream: true });
                    const lines = buffer.split("\n");
                    buffer = lines.pop() ?? "";
                    for (const line of lines) {
                        if (!line.startsWith("data: ")) continue;
                        const json = line.slice(6).trim();
                        if (!json) continue;
                        try {
                            dispatchAgentEvent(asstMsgId, JSON.parse(json) as AgentEvent);
                        } catch {
                            /* skip malformed frame */
                        }
                    }
                }
            } catch (err: unknown) {
                if ((err as Error).name !== "AbortError") {
                    updateMsg(asstMsgId, (m) => ({
                        ...m,
                        error: `Stream error: ${(err as Error).message}`,
                        isStreaming: false
                    }));
                }
            } finally {
                setIsStreaming(false);
                if (currentAsstMsgIdRef.current === asstMsgId) currentAsstMsgIdRef.current = null;
            }
        }
    }, [
        input,
        isStreaming,
        protocol,
        sessionId,
        apiBase,
        wsRef,
        sseAbortRef,
        currentAsstMsgIdRef,
        setMessages,
        setInput,
        setIsStreaming,
        dispatchAgentEvent,
        updateMsg
    ]); // eslint-disable-line react-hooks/exhaustive-deps

    // ── Derived UI values ─────────────────────────────────────────────────────
    const inputDisabled = isStreaming || (protocol === "ws" ? !wsConnected : !sessionId);
    const statusLabel =
        protocol === "ws"
            ? wsConnected
                ? "🔌 WS Connected"
                : "🔌 WS Connecting…"
            : sessionId
              ? "📡 SSE Ready"
              : "📡 SSE Connecting…";
    const statusColor = (protocol === "ws" ? wsConnected : !!sessionId) ? "var(--ac-success)" : "var(--ac-warning)";

    // ── Render ────────────────────────────────────────────────────────────────
    return (
        <div className={`agent-chat ${className}`}>
            {/* ── Header bar ── */}
            <div className="ac-header">
                <span className="ac-header__title">AI Agent Status</span>
                <div className="ac-header__controls">
                    <span className="ac-status" style={{ color: statusColor }}>
                        {statusLabel}
                    </span>
                </div>
            </div>

            {/* ── Message list ── */}
            <div className="ac-message-list">
                {messages.length === 0 && (
                    <div className="ac-empty-state">
                        <p>AI Agent ready</p>
                        <p>Connected to your Palma API tools. Ask me anything!</p>
                    </div>
                )}

                {messages.map((msg) => (
                    <MessageBubble
                        key={msg.id}
                        msg={msg}
                        onApprove={(callId) => resolveHitl(callId, true, msg.id)}
                        onReject={(callId) => resolveHitl(callId, false, msg.id)}
                    />
                ))}

                <div ref={bottomRef} />
            </div>

            {/* ── Input bar ── */}
            <div className="ac-input-bar">
                <textarea
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => {
                        if (e.key === "Enter" && !e.shiftKey) {
                            e.preventDefault();
                            void sendMessage();
                        }
                    }}
                    placeholder={isStreaming ? "Agent is thinking…" : placeholder}
                    disabled={inputDisabled}
                    rows={2}
                    className="ac-textarea"
                />
                <button
                    onClick={() => void sendMessage()}
                    disabled={inputDisabled || !input.trim()}
                    className="ac-send-btn"
                >
                    {isStreaming ? "⏳" : "➤"}
                </button>
            </div>
        </div>
    );
}
