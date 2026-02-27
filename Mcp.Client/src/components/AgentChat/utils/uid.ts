let _uid = 0;

/** Returns a stable unique string ID for message bubbles. */
export function uid(): string {
    return `msg_${++_uid}_${Date.now()}`;
}
