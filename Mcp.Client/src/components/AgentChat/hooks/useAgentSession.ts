import { useEffect, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { ChatMessage } from "../types";
import { uid } from "../utils/uid";

interface UseAgentSessionOptions {
    apiBase:     string;
    setMessages: Dispatch<SetStateAction<ChatMessage[]>>;
}

export interface UseAgentSessionReturn {
    sessionId: string | null;
}

/**
 * Creates an agent session on mount via HTTP POST and deletes it on unmount.
 *
 * The stale-closure bug (sessionId not available in cleanup) is solved with a
 * local `createdId` variable shared between the fetch callback and the cleanup
 * closure — React Strict Mode safe via the `cancelled` flag guard.
 */
export function useAgentSession({ apiBase, setMessages }: UseAgentSessionOptions): UseAgentSessionReturn {
    const [sessionId, setSessionId] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        let createdId: string | null = null;

        const deleteSession = (id: string) => {
            if (!id) return;
            fetch(`${apiBase}/api/agent/session/${id}`, { method: "DELETE" }).catch(() => { });
        };

        fetch(`${apiBase}/api/agent/session`, { method: "POST" })
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
