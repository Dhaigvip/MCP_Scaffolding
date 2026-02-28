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
    /// and injected automatically from the session's <see cref="Mcp.Palma.Contracts.PalmaContext"/>.
    /// </summary>
    public string[] ContextQueryParams { get; init; } = ["org", "ms", "branch"];

    // ── Default PalmaContext for external MCP clients (Claude Desktop, Cursor, etc.) ──
    // When an MCP client connects via /api/mcp without an AgentSession, these values
    // are used to construct a PalmaContext.  They can be overridden per-connection by
    // setting X-Palma-Version / X-Palma-Org / X-Palma-Ms / X-Palma-Branch request headers.

    /// <summary>Default API version, e.g. "1".  Required unless supplied via header.</summary>
    public string? DefaultVersion { get; init; }

    /// <summary>Default organisation identifier (org query param).  Required unless supplied via header.</summary>
    public string? DefaultOrg { get; init; }

    /// <summary>Default module-system identifier (ms query param).  Optional.</summary>
    public string? DefaultMs { get; init; }

    /// <summary>Default branch identifier (branch query param).  Optional.</summary>
    public string? DefaultBranch { get; init; }
}
