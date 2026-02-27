using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

/// <summary>
/// Takes a tool call (name + arguments dictionary) and forwards it
/// as a real HTTP request to your WebAPI.
///
/// Handles:
///   - Path parameter substitution   /orders/{id} + {id: "42"} → /orders/42
///   - Query string appending        ?status=active
///   - JSON body serialization       POST/PUT body from "body" argument key
///   - Response → string for MCP     raw JSON string returned to the LLM
/// </summary>
public sealed class SwaggerToolInvoker
{
    private readonly HttpClient _http;
    private readonly ILogger<SwaggerToolInvoker> _logger;

    public SwaggerToolInvoker(HttpClient http, ILogger<SwaggerToolInvoker> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> InvokeAsync(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        string correlationId,
        CancellationToken ct)
    {
        var url = BuildUrl(tool, arguments);
        var request = BuildRequest(tool, url, arguments, correlationId);

        _logger.LogDebug("Tool {Tool} → {Method} {Url}", tool.ToolName, tool.HttpMethod, url);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Tool {Tool} got {Status}: {Body}",
                tool.ToolName, (int)response.StatusCode, body);

            // Return structured error so the LLM understands what happened
            return JsonSerializer.Serialize(new
            {
                error = true,
                statusCode = (int)response.StatusCode,
                message = body
            });
        }

        // Try to pretty-print if JSON, otherwise return raw
        try
        {
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = false
            });
        }
        catch
        {
            return body;
        }
    }

    // ── URL building ─────────────────────────────────────────────────────────

    private static string BuildUrl(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        var path = tool.PathTemplate;

        // Substitute path parameters: /orders/{id} → /orders/42
        foreach (var p in tool.Parameters.Where(p => p.In == "path"))
        {
            if (arguments.TryGetValue(p.Name, out var val))
            {
                var strVal = val.ValueKind == JsonValueKind.String
                    ? val.GetString()!
                    : val.ToString();
                path = path.Replace($"{{{p.Name}}}", Uri.EscapeDataString(strVal));
            }
        }

        // Append query parameters
        var queryParams = tool.Parameters
            .Where(p => p.In == "query" && arguments.ContainsKey(p.Name))
            .Select(p =>
            {
                var val = arguments[p.Name];
                var strVal = val.ValueKind == JsonValueKind.String
                    ? val.GetString()!
                    : val.ToString();
                return $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(strVal)}";
            })
            .ToList();

        if (queryParams.Count > 0)
            path += "?" + string.Join("&", queryParams);

        return path;
    }

    // ── Request building ─────────────────────────────────────────────────────

    private static HttpRequestMessage BuildRequest(
        SwaggerToolDescriptor tool,
        string url,
        IReadOnlyDictionary<string, JsonElement> arguments,
        string correlationId)
    {
        var method = new HttpMethod(tool.HttpMethod);
        var request = new HttpRequestMessage(method, url);

        // Forward correlation ID to the upstream WebAPI for end-to-end tracing
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        // Attach body for mutating methods
        if (tool.HttpMethod is "POST" or "PUT" or "PATCH")
        {
            if (arguments.Count > 0)
            {
                var bodyJson = JsonSerializer.Serialize(arguments);

                request.Content = new StringContent(
                    bodyJson,
                    Encoding.UTF8,
                    "application/json"
                );
            }
        }

        return request;
    }
}