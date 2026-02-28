namespace Mcp.Palma;

/// <summary>
/// Configuration for the Palma → MCP in-process tool pipeline.
/// Bound from appsettings.json under "PalmaMcp".
/// </summary>
public sealed class PalmaMcpOptions
{
    /// <summary>
    /// Query parameter names that carry per-session Palma context.
    /// These are stripped from tool InputSchemas (the LLM never sees them)
    /// and injected automatically by <see cref="PalmaToolInvoker"/> from the
    /// session's <see cref="PalmaContext"/>.
    /// </summary>
    public string[] ContextQueryParams { get; init; } = ["org", "ms", "branch"];
}
