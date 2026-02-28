namespace Mcp.Palma.Contracts;

/// <summary>
/// Describes a single query parameter for a Palma endpoint.
/// </summary>
public sealed class PalmaQueryParamInfo
{
    public required string Name { get; init; }
    public bool Required { get; init; }
    /// <summary>JSON Schema primitive type: "string", "integer", "boolean", etc.</summary>
    public string Type { get; init; } = "string";
    public string? Description { get; init; }
    /// <summary>Example value shown in tool documentation.</summary>
    public string? ExampleValue { get; init; }
}
