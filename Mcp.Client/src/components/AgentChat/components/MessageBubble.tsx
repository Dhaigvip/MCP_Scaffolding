import { useState } from "react";
import type { ChatMessage } from "../types";
import { ToolCallCard } from "./ToolCallCard";
import { HitlDialog } from "./HitlDialog";

export interface MessageBubbleProps {
    msg: ChatMessage;
    onApprove: (callId: string) => void;
    onReject: (callId: string) => void;
}

export function MessageBubble({ msg, onApprove, onReject }: MessageBubbleProps) {
    const [showThinking, setShowThinking] = useState(false);

    if (msg.role === "user")
        return <div className="ac-bubble ac-bubble--user">{msg.text}</div>;

    if (msg.role === "system")
        return <div className="ac-bubble ac-bubble--system">⚠️ {msg.error}</div>;

    // Assistant bubble
    return (
        <div className="ac-bubble ac-bubble--assistant">

            {/* Thinking block */}
            {msg.thinking && (
                <div className="ac-thinking-wrapper">
                    <button onClick={() => setShowThinking(v => !v)} className="ac-thinking-toggle">
                        🧠 {showThinking ? "Hide thinking" : "Show thinking"}
                    </button>
                    {showThinking && <pre className="ac-thinking-content">{msg.thinking}</pre>}
                </div>
            )}

            {/* Brief status line (shown while streaming, before text arrives) */}
            {msg.status && !msg.text && (
                <div className="ac-status-line">{msg.status}</div>
            )}

            {/* Text response */}
            {msg.text && (
                <div className="ac-text-body">
                    {msg.text}
                    {msg.isStreaming && <span className="ac-cursor">▋</span>}
                </div>
            )}

            {/* Tool calls */}
            {msg.toolCalls?.map((tc, i) => <ToolCallCard key={i} tc={tc} />)}

            {/* HITL approval dialog (pending) */}
            {msg.hitl?.status === "pending" && (
                <HitlDialog
                    hitl={msg.hitl}
                    onApprove={() => onApprove(msg.hitl!.callId)}
                    onReject={() => onReject(msg.hitl!.callId)}
                />
            )}

            {/* HITL resolved indicator */}
            {msg.hitl && msg.hitl.status !== "pending" && (
                <div
                    className="ac-hitl-resolved"
                    style={{ color: msg.hitl.status === "approved" ? "var(--ac-success)" : "var(--ac-danger)" }}
                >
                    {msg.hitl.status === "approved" ? "✅ Approved" : "❌ Rejected"}: {msg.hitl.toolName}
                </div>
            )}

            {/* Error */}
            {msg.error && <div className="ac-error-badge">❌ {msg.error}</div>}
        </div>
    );
}
