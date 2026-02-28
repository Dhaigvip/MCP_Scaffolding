export type {
    AgentEvent, RiskInfo, Protocol,
    ThinkingEvent, StatusEvent, TextDeltaEvent, TextEndEvent,
    HitlEvent, ToolAutoEvent, ToolResultEvent, ToolSkippedEvent,
    DoneEvent, ErrorEvent
} from "./events";

export type { ChatMessage, HitlRecord, ToolCallRecord, MessageRole } from "./messages";

/** Per-session Palma context — mirrors the C# PalmaContext record. */
export interface PalmaContext {
    /** API version, e.g. "1" → /API/v1/... */
    version: string;
    /** Organization identifier (query param: org). */
    org: string;
    /** Module system identifier (query param: ms). Optional. */
    ms?: string;
    /** Branch identifier (query param: branch). Optional. */
    branch?: string;
}
