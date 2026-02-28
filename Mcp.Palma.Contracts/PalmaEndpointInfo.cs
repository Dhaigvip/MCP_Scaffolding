namespace Mcp.Palma.Contracts;

/// <summary>
/// Lightweight DTO describing one Palma endpoint — no Palma-specific types,
/// so the Mcp.Server project stays decoupled from PalmaServer internals.
/// </summary>
public sealed class PalmaEndpointInfo
{
    /// <summary>URI fragment registered in SemVerHandlers, e.g. "GET/modules".</summary>
    public required string Uri { get; init; }

    /// <summary>Normalised HTTP method. "BOTH" or "GET" → callers should use "POST".</summary>
    public string HttpMethod { get; init; } = "POST";

    /// <summary>Short summary shown to the LLM as the tool description.</summary>
    public string? Summary { get; init; }

    /// <summary>Long description (optional, supplementary to Summary).</summary>
    public string? Description { get; init; }

    /// <summary>Query parameters accepted by this endpoint (includes context params).</summary>
    public List<PalmaQueryParamInfo> QueryParams { get; init; } = [];
}
