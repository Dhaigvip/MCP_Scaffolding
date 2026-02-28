using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Mcp.Palma;

/// <summary>
/// Acquires and caches a bearer token from the identity provider using the
/// OAuth2 client credentials flow.
///
/// Thread-safe: a <see cref="SemaphoreSlim"/> prevents concurrent token requests
/// while a double-checked validity test avoids acquiring the lock on the hot path.
/// The cached token is renewed 60 seconds before its stated expiry.
/// </summary>
public sealed class PalmaTokenProvider
{
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly PalmaRemoteMcpOptions _options;
    private readonly ILogger<PalmaTokenProvider> _logger;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PalmaTokenProvider(
        HttpClient http,
        PalmaRemoteMcpOptions options,
        ILogger<PalmaTokenProvider> logger)
    {
        _http    = http;
        _options = options;
        _logger  = logger;
    }

    /// <summary>
    /// Returns a valid bearer token, refreshing from the IDP if the cached one
    /// has expired (or is within the 60-second expiry buffer).
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        // Fast path — token still valid, no locking needed
        if (IsValid()) return _cachedToken!;

        await _lock.WaitAsync(ct);
        try
        {
            // Double-check: another thread may have refreshed while we waited
            if (IsValid()) return _cachedToken!;

            _logger.LogInformation("Requesting bearer token from {Endpoint}", _options.TokenEndpoint);

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "client_credentials",
                ["client_id"]     = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"]         = _options.Scope
            });

            using var response = await _http.PostAsync(_options.TokenEndpoint, form, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _cachedToken = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException(
                    $"Token response from '{_options.TokenEndpoint}' is missing 'access_token'.");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _tokenExpiry  = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            _logger.LogInformation("Bearer token acquired, expires in {ExpiresIn}s.", expiresIn);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsValid() =>
        _cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry - ExpiryBuffer;
}
