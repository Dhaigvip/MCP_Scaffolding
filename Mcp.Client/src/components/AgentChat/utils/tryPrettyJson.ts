/** Attempts JSON.parse + JSON.stringify with indent; returns original string on failure. */
export function tryPrettyJson(str: string): string {
    try {
        return JSON.stringify(JSON.parse(str) as unknown, null, 2);
    } catch {
        return str;
    }
}
