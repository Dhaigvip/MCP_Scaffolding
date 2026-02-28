using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Palma.Contracts;
using Mcp.Swagger;
using Microsoft.Extensions.Logging;

namespace Mcp.Palma;

/// <summary>
/// Remote tool loader: discovers Palma endpoints by calling
///   GET {ApiBaseUrl}{EndpointsPath}   (default: /api/mcp/endpoints)
/// on the remote Palma web API using OAuth2 client-credentials authentication.
///
/// The Palma web API must expose this endpoint — see <see cref="IPalmaEndpointSource"/>.
/// Used in the separate-process scenario.
/// </summary>
public sealed class PalmaRemoteToolLoader : IToolLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly PalmaRemoteMcpOptions _options;
    private readonly PalmaTokenProvider _tokens;
    private readonly ILogger<PalmaRemoteToolLoader> _logger;

    public PalmaRemoteToolLoader(
        HttpClient http,
        PalmaRemoteMcpOptions options,
        PalmaTokenProvider tokens,
        ILogger<PalmaRemoteToolLoader> logger)
    {
        _http = http;
        _options = options;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<List<SwaggerToolDescriptor>> LoadAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Loading Palma endpoints from {Base}{Path}",
            _options.ApiBaseUrl, _options.EndpointsPath);

        var token = await _tokens.GetTokenAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, _options.EndpointsPath);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("AuthScheme", _options.SchemeId);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var endpoints = JsonSerializer.Deserialize<List<PalmaEndpointInfo>>(json, JsonOptions) ?? [];

        var contextParams = new HashSet<string>(
            _options.ContextQueryParams, StringComparer.OrdinalIgnoreCase);

        return PalmaToolBuilder.Build(endpoints, contextParams, _logger);
    }
}
