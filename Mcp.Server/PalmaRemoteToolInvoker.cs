using System.Text.Json;
using Mcp.Swagger;
using Microsoft.Extensions.Logging;

namespace Mcp.Palma;

/// <summary>
/// Remote tool invoker: forwards tool calls to the Palma web API over HTTP,
/// authenticating with a bearer token (OAuth2 client credentials).
///
/// URL pattern:
///   POST {ApiBaseUrl}/API/v{version}/{uri}?org=...&amp;ms=...&amp;branch=...&amp;{llm-args}
///
/// Auth headers on every request:
///   Authorization : Bearer {token}
///   AuthScheme    : {SchemeId}
///
/// Used in the separate-process scenario.
/// </summary>
public sealed class PalmaRemoteToolInvoker : IToolInvoker
{
    private readonly HttpClient _http;
    private readonly PalmaRemoteMcpOptions _options;
    private readonly PalmaTokenProvider _tokens;
    private readonly ILogger<PalmaRemoteToolInvoker> _logger;

    public PalmaRemoteToolInvoker(
        HttpClient http,
        PalmaRemoteMcpOptions options,
        PalmaTokenProvider tokens,
        ILogger<PalmaRemoteToolInvoker> logger)
    {
        _http    = http;
        _options = options;
        _tokens  = tokens;
        _logger  = logger;
    }

    public async Task<string> InvokeAsync(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        PalmaContext? palmaContext,
        string correlationId,
        CancellationToken ct)
    {
        if (palmaContext is null)
            throw new InvalidOperationException(
                $"PalmaContext is required to invoke Palma tool '{tool.ToolName}'. " +
                "Ensure the client passes version, org, ms, and branch at session creation.");

        var url   = BuildUrl(tool, arguments, palmaContext);
        var token = await _tokens.GetTokenAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("AuthScheme",       _options.SchemeId);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        _logger.LogDebug("Palma remote tool {Tool} → POST {Url}", tool.ToolName, url);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Palma tool {Tool} got {Status}: {Body}",
                tool.ToolName, (int)response.StatusCode, body);

            return JsonSerializer.Serialize(new
            {
                error      = true,
                statusCode = (int)response.StatusCode,
                message    = body
            });
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc.RootElement);
        }
        catch
        {
            return body;
        }
    }

    // ── URL building ─────────────────────────────────────────────────────────

    private static string BuildUrl(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        PalmaContext ctx)
    {
        // PathTemplate is the raw Palma URI fragment, e.g. "GET/modules"
        var path = $"/API/v{ctx.Version}/{tool.PathTemplate}";

        var queryParts = new List<string>
        {
            $"org={Uri.EscapeDataString(ctx.Org)}"
        };

        if (!string.IsNullOrEmpty(ctx.Ms))
            queryParts.Add($"ms={Uri.EscapeDataString(ctx.Ms)}");

        if (!string.IsNullOrEmpty(ctx.Branch))
            queryParts.Add($"branch={Uri.EscapeDataString(ctx.Branch)}");

        // Append LLM-supplied query params (context params already stripped from schema)
        foreach (var p in tool.Parameters.Where(p => p.In == "query"))
        {
            if (!arguments.TryGetValue(p.Name, out var val)) continue;
            var strVal = val.ValueKind == JsonValueKind.String
                ? val.GetString()!
                : val.ToString();
            queryParts.Add($"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(strVal)}");
        }

        return queryParts.Count > 0
            ? path + "?" + string.Join("&", queryParts)
            : path;
    }
}
