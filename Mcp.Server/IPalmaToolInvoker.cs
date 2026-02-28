namespace Mcp.Palma;

/// <summary>
/// In-process invocation contract for Palma endpoints.
/// Implemented by the host Palma web API (e.g. via SemVerHandlers.HandleRequest)
/// so that tool calls never leave the process — no HTTP loopback overhead.
///
/// This interface lives in Mcp.Server so the MCP library can depend on it;
/// the implementation lives in the Palma web API and is registered at startup.
/// </summary>
public interface IPalmaToolInvoker
{
    /// <param name="uri">
    ///   Palma endpoint URI fragment, e.g. <c>"GET/modules"</c> — exactly as
    ///   registered in <c>SemVerHandlers</c>.
    /// </param>
    /// <param name="context">
    ///   Session-level Palma context (version, org, ms, branch).
    /// </param>
    /// <param name="additionalParams">
    ///   Extra query parameters supplied by the LLM (already stripped of context params).
    ///   Keys and values are plain strings — the implementation appends them as needed.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON string response to feed back to the LLM.</returns>
    Task<string> InvokeAsync(
        string uri,
        PalmaContext context,
        IReadOnlyDictionary<string, string> additionalParams,
        CancellationToken ct);
}
