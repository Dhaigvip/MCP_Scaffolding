namespace Mcp.Palma.Contracts;

/// <summary>
/// Implemented by the host Palma web API to expose its registered endpoints to
/// <see cref="PalmaToolLoader"/>.  This interface lives in Mcp.Server so the
/// MCP library can depend on it without knowing about Palma internals;
/// the implementation lives in the Palma web API and is registered at startup.
/// </summary>
public interface IPalmaEndpointSource
{
    IEnumerable<PalmaEndpointInfo> GetEndpoints();
}
