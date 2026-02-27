import type { RiskInfo } from "./events";

export type MessageRole = "user" | "assistant" | "system";

export interface ToolCallRecord {
    toolName: string;
    input: Record<string, unknown>;
    result?: string;
    isError?: boolean;
    skipped?: boolean;
    auto?: boolean;
}

export interface HitlRecord {
    callId: string;
    toolName: string;
    input: Record<string, unknown>;
    risk: RiskInfo;
    status: "pending" | "approved" | "rejected";
}

export interface ChatMessage {
    id: string;
    role: MessageRole;
    text?: string;
    thinking?: string;
    status?: string;
    toolCalls?: ToolCallRecord[];
    hitl?: HitlRecord;
    error?: string;
    isStreaming?: boolean;
}
