import { useState } from "react";
import type { HitlRecord } from "../types";

interface HitlDialogProps {
    hitl: HitlRecord;
    onApprove: () => void;
    onReject: () => void;
}

export function HitlDialog({ hitl, onApprove, onReject }: HitlDialogProps) {
    const [showInput, setShowInput] = useState(false);

    const borderColor = hitl.risk.color === "red"    ? "var(--ac-danger)"
        : hitl.risk.color === "yellow" ? "var(--ac-warning)"
        : "var(--ac-success)";

    return (
        <div className="ac-hitl-box" style={{ borderColor }}>
            <div className="ac-hitl-box__header">
                <span style={{ fontSize: 18 }}>{hitl.risk.emoji}</span>
                <strong style={{ color: borderColor }}>{hitl.risk.label}</strong>
                <span style={{ fontFamily: "var(--ac-font-mono)", fontSize: 13 }}>— {hitl.toolName}</span>
            </div>

            {hitl.risk.warning && (
                <div className="ac-hitl-box__warning">{hitl.risk.warning}</div>
            )}

            <button onClick={() => setShowInput(v => !v)} className="ac-expand-btn">
                {showInput ? "▲ Hide details" : "▼ View tool input"}
            </button>
            {showInput && <pre className="ac-code-block">{JSON.stringify(hitl.input, null, 2)}</pre>}

            <div className="ac-hitl-box__actions">
                <button
                    onClick={onApprove}
                    className="ac-action-btn"
                    style={{
                        background: hitl.risk.defaultApprove ? "#16a34a" : "#374151",
                        outline: hitl.risk.defaultApprove ? "2px solid var(--ac-success)" : "none"
                    }}
                >
                    ✅ Approve {hitl.risk.defaultApprove ? "(default)" : ""}
                </button>
                <button
                    onClick={onReject}
                    className="ac-action-btn"
                    style={{
                        background: !hitl.risk.defaultApprove ? "#b91c1c" : "#374151",
                        outline: !hitl.risk.defaultApprove ? "2px solid var(--ac-danger)" : "none"
                    }}
                >
                    ❌ Reject {!hitl.risk.defaultApprove ? "(default)" : ""}
                </button>
            </div>
        </div>
    );
}
