// Mirror AgentEvent.cs discriminator values from the .NET server

export interface ThinkingEvent    { type: "thinking";     content: string; }
export interface StatusEvent      { type: "status";       message: string; }
export interface TextDeltaEvent   { type: "text_delta";   delta: string; }
export interface TextEndEvent     { type: "text_end"; }
export interface HitlEvent {
    type: "hitl";
    callId: string;
    toolName: string;
    input: Record<string, unknown>;
    risk: RiskInfo;
}
export interface ToolAutoEvent    { type: "tool_auto";    toolName: string; input: Record<string, unknown>; }
export interface ToolResultEvent  { type: "tool_result";  toolName: string; content: string; isError: boolean; }
export interface ToolSkippedEvent { type: "tool_skipped"; toolName: string; }
export interface DoneEvent        { type: "done"; }
export interface ErrorEvent       { type: "error";        message: string; }

export type AgentEvent =
    | ThinkingEvent | StatusEvent | TextDeltaEvent | TextEndEvent
    | HitlEvent | ToolAutoEvent | ToolResultEvent | ToolSkippedEvent
    | DoneEvent | ErrorEvent;

export interface RiskInfo {
    level: "high" | "medium" | "low" | "readonly";
    label: string;
    color: "red" | "yellow" | "green" | "blue";
    emoji: string;
    requiresApproval: boolean;
    defaultApprove: boolean;
    warning?: string;
}

export type Protocol = "ws" | "sse";
