import type { MutableRefObject } from "react";
import type { ChatMessage, Protocol } from "../types";

interface UseHitlResolutionOptions {
    protocol:           Protocol;
    sessionId:          string | null;
    apiBase:            string;
    wsRef:              MutableRefObject<WebSocket | null>;
    pendingApprovalRef: MutableRefObject<string | null>;
    updateMsg:          (id: string, updater: (m: ChatMessage) => ChatMessage) => void;
}

export interface UseHitlResolutionReturn {
    resolveHitl: (callId: string, approved: boolean, asstMsgId: string) => void;
}

/**
 * Returns a stable callback that approves or rejects a pending HITL tool call.
 * - WebSocket mode: sends approval on the same WS connection.
 * - SSE mode: fires a separate HTTP POST; the open SSE stream then resumes.
 */
export function useHitlResolution({
    protocol, sessionId, apiBase,
    wsRef, pendingApprovalRef, updateMsg
}: UseHitlResolutionOptions): UseHitlResolutionReturn {

    function resolveHitl(callId: string, approved: boolean, asstMsgId: string) {
        // Optimistic UI update
        updateMsg(asstMsgId, m => ({
            ...m,
            hitl: m.hitl ? { ...m.hitl, status: approved ? "approved" : "rejected" } : undefined
        }));
        pendingApprovalRef.current = null;

        if (protocol === "ws") {
            wsRef.current?.send(JSON.stringify({ type: "approve", callId, approved }));
        } else {
            fetch(`${apiBase}/api/agent/approve`, {
                method:  "POST",
                headers: { "Content-Type": "application/json" },
                body:    JSON.stringify({ sessionId, callId, approved })
            }).catch(console.error);
        }
    }

    return { resolveHitl };
}
