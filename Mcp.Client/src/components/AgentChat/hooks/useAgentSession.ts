import { useEffect, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { ChatMessage, PalmaContext } from "../types";
import { uid } from "../utils/uid";

interface UseAgentSessionOptions {
    apiBase:      string;
    setMessages:  Dispatch<SetStateAction<ChatMessage[]>>;
    palmaContext?: PalmaContext;
}

export interface UseAgentSessionReturn {
    sessionId: string | null;
}

/**
 * Creates an agent session on mount via HTTP POST and deletes it on unmount.
 *
 * When `palmaContext` is provided it is sent in the POST body so the server
 * can attach it to the session (version, org, ms, branch) and inject it into
 * every Palma tool call without exposing it to the LLM.
 *
 * The stale-closure bug (sessionId not available in cleanup) is solved with a
 * local `createdId` variable shared between the fetch callback and the cleanup
 * closure — React Strict Mode safe via the `cancelled` flag guard.
 */
export function useAgentSession({ apiBase, setMessages, palmaContext }: UseAgentSessionOptions): UseAgentSessionReturn {
    const [sessionId, setSessionId] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        let createdId: string | null = null;

        const deleteSession = (id: string) => {
            if (!id) return;
            fetch(`${apiBase}/api/agent/session/${id}`, { method: "DELETE" }).catch(() => { });
        };

        const body = palmaContext
            ? JSON.stringify({ palmaContext })
            : undefined;

        fetch(`${apiBase}/api/agent/session`, {
            method: "POST",
            headers: body ? { "Content-Type": "application/json" } : undefined,
            body
        })
            .then(r => r.json())
            .then(({ sessionId: id }: { sessionId: string }) => {
                if (!cancelled) {
                    createdId = id;
                    setSessionId(id);
                } else {
                    // React Strict Mode: cleanup already ran — delete the orphan session.
                    deleteSession(id);
                }
            })
            .catch((err: Error) => {
                if (!cancelled)
                    setMessages(prev => [
                        ...prev,
                        { id: uid(), role: "system", error: `Failed to create session: ${err.message}` }
                    ]);
            });

        return () => {
            cancelled = true;
            setSessionId(null);
            if (createdId) {
                deleteSession(createdId);
                createdId = null;
            }
        };
    }, [apiBase]); // eslint-disable-line react-hooks/exhaustive-deps

    return { sessionId };
}
