namespace Mcp.Palma;

/// <summary>
/// Per-session Palma context supplied by the client at session creation.
/// Injected by <see cref="PalmaToolInvoker"/> into every API call as query
/// parameters and path version — never exposed in tool InputSchemas.
/// </summary>
public sealed record PalmaContext(
    /// <summary>API version, e.g. "1" → /API/v1/...</summary>
    string Version,
    /// <summary>Organization identifier (query param: org).</summary>
    string Org,
    /// <summary>Module system identifier (query param: ms). Optional.</summary>
    string? Ms,
    /// <summary>Branch identifier (query param: branch). Optional.</summary>
    string? Branch);
