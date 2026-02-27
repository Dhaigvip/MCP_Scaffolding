import { useState } from "react";
import type { ToolCallRecord } from "../types";
import { tryPrettyJson } from "../utils/tryPrettyJson";

interface ToolCallCardProps {
    tc: ToolCallRecord;
}

export function ToolCallCard({ tc }: ToolCallCardProps) {
    const [showInput, setShowInput] = useState(false);
    const [showResult, setShowResult] = useState(false);

    const borderColor = tc.isError ? "var(--ac-danger)" : tc.skipped ? "var(--ac-skipped)" : "var(--ac-success)";
    const badge = tc.skipped ? "⏭ Skipped" : tc.auto ? "⚡ Auto" : tc.isError ? "❌ Error" : "✅ Done";

    return (
        <div className="ac-tool-card" style={{ borderLeftColor: borderColor }}>
            <div className="ac-tool-card__header">
                <span className="ac-tool-card__name">🔧 {tc.toolName}</span>
                <span className="ac-tool-card__badge">{badge}</span>
            </div>

            {Object.keys(tc.input).length > 0 && (
                <button onClick={() => setShowInput(v => !v)} className="ac-expand-btn">
                    {showInput ? "▲ Hide input" : "▼ Input"}
                </button>
            )}
            {showInput && <pre className="ac-code-block">{JSON.stringify(tc.input, null, 2)}</pre>}

            {tc.result && (<>
                <button onClick={() => setShowResult(v => !v)} className="ac-expand-btn">
                    {showResult ? "▲ Hide result" : "▼ Result"}
                </button>
                {showResult && (
                    <pre className="ac-code-block" style={{ borderLeft: `3px solid ${borderColor}` }}>
                        {tryPrettyJson(tc.result)}
                    </pre>
                )}
            </>)}
        </div>
    );
}
