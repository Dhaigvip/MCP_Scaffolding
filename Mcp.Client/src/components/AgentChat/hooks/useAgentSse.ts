import { useEffect, useRef } from "react";
import type { MutableRefObject } from "react";
import type { Protocol } from "../types";

interface UseAgentSseOptions {
    protocol: Protocol;
}

export interface UseAgentSseReturn {
    sseAbortRef: MutableRefObject<AbortController | null>;
}

/**
 * Owns the AbortController for in-flight SSE streams.
 * Aborts any open stream automatically when switching back to WebSocket mode.
 */
export function useAgentSse({ protocol }: UseAgentSseOptions): UseAgentSseReturn {
    const sseAbortRef = useRef<AbortController | null>(null);

    useEffect(() => {
        if (protocol === "ws") {
            sseAbortRef.current?.abort();
            sseAbortRef.current = null;
        }
    }, [protocol]);

    return { sseAbortRef };
}
