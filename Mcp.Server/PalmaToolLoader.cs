using Mcp.Palma.Contracts;
using Mcp.Swagger;
using Microsoft.Extensions.Logging;

namespace Mcp.Palma;

/// <summary>
/// In-process tool loader: reads endpoint metadata directly from
/// <see cref="IPalmaEndpointSource"/> (implemented by the host Palma web API)
/// and converts it to <see cref="SwaggerToolDescriptor"/> objects for the MCP registry.
///
/// Used in the co-hosted scenario — no HTTP involved.
/// </summary>
public sealed class PalmaToolLoader : IToolLoader
{
    private readonly IPalmaEndpointSource _source;
    private readonly PalmaMcpOptions _options;
    private readonly ILogger<PalmaToolLoader> _logger;

    public PalmaToolLoader(
        IPalmaEndpointSource source,
        PalmaMcpOptions options,
        ILogger<PalmaToolLoader> logger)
    {
        _source  = source;
        _options = options;
        _logger  = logger;
    }

    public Task<List<SwaggerToolDescriptor>> LoadAsync(CancellationToken ct = default)
    {
        var contextParams = new HashSet<string>(
            _options.ContextQueryParams, StringComparer.OrdinalIgnoreCase);

        var tools = PalmaToolBuilder.Build(_source.GetEndpoints(), contextParams, _logger);
        return Task.FromResult(tools);
    }
}
