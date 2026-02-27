import { useCallback, useRef, useState } from "react";
import type { Dispatch, MutableRefObject, SetStateAction } from "react";
import type { AgentEvent, ChatMessage } from "../types";

export interface UseAgentMessagesReturn {
    messages:            ChatMessage[];
    setMessages:         Dispatch<SetStateAction<ChatMessage[]>>;
    isStreaming:         boolean;
    setIsStreaming:      Dispatch<SetStateAction<boolean>>;
    currentAsstMsgIdRef: MutableRefObject<string | null>;
    pendingApprovalRef:  MutableRefObject<string | null>;
    updateMsg:           (id: string, updater: (m: ChatMessage) => ChatMessage) => void;
    dispatchAgentEvent:  (msgId: string, event: AgentEvent) => void;
}

export function useAgentMessages(): UseAgentMessagesReturn {
    const [messages,    setMessages]    = useState<ChatMessage[]>([]);
    const [isStreaming, setIsStreaming] = useState(false);

    const currentAsstMsgIdRef = useRef<string | null>(null);
    const pendingApprovalRef  = useRef<string | null>(null);

    // Stable helper — setMessages identity is guaranteed by React, so [] deps is correct.
    const updateMsg = useCallback((id: string, updater: (m: ChatMessage) => ChatMessage) => {
        setMessages(prev => prev.map(m => m.id === id ? updater(m) : m));
    }, []);

    // ── Agent event dispatcher ────────────────────────────────────────────────
    // All server-push events land here. Only calls stable setters and mutates
    // refs, so it never needs to be in any hook dep array.
    function dispatchAgentEvent(msgId: string, event: AgentEvent) {
        switch (event.type) {
            case "thinking":
                updateMsg(msgId, m => ({ ...m, thinking: (m.thinking ?? "") + event.content }));
                break;

            case "status":
                updateMsg(msgId, m => ({ ...m, status: event.message }));
                break;

            case "text_delta":
                updateMsg(msgId, m => ({ ...m, text: (m.text ?? "") + event.delta }));
                break;

            case "text_end":
                // Text accumulation complete; nothing extra needed.
                break;

            case "tool_auto":
                updateMsg(msgId, m => ({
                    ...m,
                    toolCalls: [...(m.toolCalls ?? []), { toolName: event.toolName, input: event.input, auto: true }]
                }));
                break;

            case "hitl":
                pendingApprovalRef.current = event.callId;
                updateMsg(msgId, m => ({
                    ...m,
                    hitl: {
                        callId:   event.callId,
                        toolName: event.toolName,
                        input:    event.input,
                        risk:     event.risk,
                        status:   "pending"
                    }
                }));
                break;

            case "tool_result": {
                updateMsg(msgId, m => {
                    const calls = m.toolCalls ?? [];
                    const idx = [...calls].reverse().findIndex(
                        t => t.toolName === event.toolName && !t.result && !t.skipped);
                    if (idx === -1) {
                        return {
                            ...m,
                            toolCalls: [...calls, {
                                toolName: event.toolName, input: {},
                                result: event.content, isError: event.isError
                            }]
                        };
                    }
                    const real = calls.length - 1 - idx;
                    const updated = [...calls];
                    updated[real] = { ...updated[real], result: event.content, isError: event.isError };
                    return { ...m, toolCalls: updated };
                });
                break;
            }

            case "tool_skipped":
                updateMsg(msgId, m => ({
                    ...m,
                    hitl:      m.hitl ? { ...m.hitl, status: "rejected" } : undefined,
                    toolCalls: [...(m.toolCalls ?? []), { toolName: event.toolName, input: {}, skipped: true }]
                }));
                break;

            case "done":
                updateMsg(msgId, m => ({ ...m, isStreaming: false }));
                currentAsstMsgIdRef.current = null;
                setIsStreaming(false);
                break;

            case "error":
                updateMsg(msgId, m => ({ ...m, error: event.message, isStreaming: false }));
                currentAsstMsgIdRef.current = null;
                setIsStreaming(false);
                break;
        }
    }

    return {
        messages, setMessages,
        isStreaming, setIsStreaming,
        currentAsstMsgIdRef, pendingApprovalRef,
        updateMsg, dispatchAgentEvent
    };
}
