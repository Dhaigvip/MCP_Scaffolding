import { useEffect, useRef, useState } from "react";
import type { Dispatch, MutableRefObject, SetStateAction } from "react";
import type { AgentEvent, ChatMessage, Protocol } from "../types";
import { uid } from "../utils/uid";

interface UseAgentWebSocketOptions {
    apiBase:             string;
    sessionId:           string | null;
    protocol:            Protocol;
    currentAsstMsgIdRef: MutableRefObject<string | null>;
    dispatchAgentEvent:  (msgId: string, event: AgentEvent) => void;
    setIsStreaming:      Dispatch<SetStateAction<boolean>>;
    updateMsg:           (id: string, updater: (m: ChatMessage) => ChatMessage) => void;
    setMessages:         Dispatch<SetStateAction<ChatMessage[]>>;
}

export interface UseAgentWebSocketReturn {
    wsRef:       MutableRefObject<WebSocket | null>;
    wsConnected: boolean;
}

/**
 * Opens a WebSocket when sessionId is available and protocol === "ws".
 * Closes on cleanup or protocol switch.
 *
 * React Strict Mode double-invocation fix:
 *   Defers socket creation by one scheduler tick via setTimeout(fn, 0).
 *   If cleanup fires before the tick the timer is cancelled — no socket
 *   is created at all, so the second (real) mount connects cleanly.
 */
export function useAgentWebSocket({
    apiBase, sessionId, protocol,
    currentAsstMsgIdRef, dispatchAgentEvent,
    setIsStreaming, updateMsg, setMessages
}: UseAgentWebSocketOptions): UseAgentWebSocketReturn {
    const wsRef        = useRef<WebSocket | null>(null);
    const [wsConnected, setWsConnected] = useState(false);

    useEffect(() => {
        if (!sessionId || protocol !== "ws") return;

        const wsBase = apiBase
            ? apiBase.replace(/^http/, "ws")
            : `${window.location.protocol === "https:" ? "wss" : "ws"}://${window.location.host}`;

        let alive = true;

        const timer = setTimeout(() => {
            if (!alive) return;

            const ws = new WebSocket(`${wsBase}/api/agent/ws/${sessionId}`);
            wsRef.current = ws;

            ws.onopen = () => { if (alive) setWsConnected(true); };

            ws.onclose = () => {
                if (!alive) return;
                setWsConnected(false);
                wsRef.current = null;
                const id = currentAsstMsgIdRef.current;
                if (id) {
                    updateMsg(id, m => ({ ...m, isStreaming: false }));
                    currentAsstMsgIdRef.current = null;
                    setIsStreaming(false);
                }
            };

            ws.onerror = () => {
                if (!alive) return;
                setMessages(prev => [...prev, {
                    id: uid(), role: "system",
                    error: "WebSocket connection error — is the .NET server running?"
                }]);
            };

            ws.onmessage = (e: MessageEvent) => {
                let evt: AgentEvent;
                try { evt = JSON.parse(e.data as string); } catch { return; }
                const msgId = currentAsstMsgIdRef.current;
                if (msgId) dispatchAgentEvent(msgId, evt);
            };
        }, 0);

        return () => {
            alive = false;
            clearTimeout(timer);
            if (wsRef.current) {
                wsRef.current.close();
                wsRef.current = null;
                setWsConnected(false);
            }
        };
    }, [sessionId, apiBase, protocol]); // eslint-disable-line react-hooks/exhaustive-deps

    return { wsRef, wsConnected };
}
