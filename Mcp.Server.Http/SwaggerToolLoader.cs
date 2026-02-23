using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Mcp.Swagger;

public sealed class SwaggerToolLoader
{
    private readonly HttpClient _http;
    private readonly ILogger<SwaggerToolLoader> _logger;

    public SwaggerToolLoader(HttpClient http, ILogger<SwaggerToolLoader> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<SwaggerToolDescriptor>> LoadAsync(
        string swaggerUrl,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching OpenAPI spec from {Url}", swaggerUrl);

        var json = await _http.GetStringAsync(swaggerUrl, ct);
        using var doc = JsonDocument.Parse(json);

        // Keep root alive for $ref resolution — clone it so we own the memory
        var root  = doc.RootElement.Clone();
        var tools = new List<SwaggerToolDescriptor>();

        if (!root.TryGetProperty("paths", out var paths))
        {
            _logger.LogWarning("No 'paths' found in swagger document.");
            return tools;
        }

        foreach (var pathProp in paths.EnumerateObject())
        {
            var pathTemplate = pathProp.Name;

            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                var httpMethod = methodProp.Name.ToUpperInvariant();
                var operation  = methodProp.Value;

                if (!IsSupportedMethod(httpMethod)) continue;

                var toolName    = BuildToolName(httpMethod, pathTemplate, operation);
                var description = GetString(operation, "summary")
                               ?? GetString(operation, "description")
                               ?? toolName;

                var parameters = ParseParameters(operation);
                var body       = ParseBody(operation, root);
                var schema     = BuildInputSchema(parameters, body);

                tools.Add(new SwaggerToolDescriptor
                {
                    ToolName     = toolName,
                    Description  = description,
                    HttpMethod   = httpMethod,
                    PathTemplate = pathTemplate,
                    Parameters   = parameters,
                    Body         = body,
                    InputSchema  = schema
                });

                _logger.LogInformation(
                    "Registered tool [{ToolName}] ← {Method} {Path} | {Count} input properties",
                    toolName, httpMethod, pathTemplate, schema.Properties.Count);
            }
        }

        _logger.LogInformation("Loaded {Count} tools from OpenAPI spec.", tools.Count);
        return tools;
    }

    // ── Tool name ─────────────────────────────────────────────────────────────

    private static bool IsSupportedMethod(string method) =>
        method is "GET" or "POST" or "PUT" or "DELETE" or "PATCH";

    private static string BuildToolName(string method, string path, JsonElement operation)
    {
        if (operation.TryGetProperty("operationId", out var opId) &&
            opId.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(opId.GetString()))
        {
            return opId.GetString()!;   // preserve original casing e.g. "AddNumbers"
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.StartsWith('{') ? s.Trim('{', '}') : s);

        return method.ToLowerInvariant() + "_" + string.Join("_", segments);
    }

    // ── Parameters ────────────────────────────────────────────────────────────

    private static List<SwaggerParameterDescriptor> ParseParameters(JsonElement operation)
    {
        var result = new List<SwaggerParameterDescriptor>();
        if (!operation.TryGetProperty("parameters", out var parameters)) return result;

        foreach (var p in parameters.EnumerateArray())
        {
            var inVal = GetString(p, "in") ?? "query";
            if (inVal == "body") continue;

            var schema = p.TryGetProperty("schema", out var s) ? s : default;
            var type   = schema.ValueKind == JsonValueKind.Object
                ? GetString(schema, "type") ?? "string"
                : "string";

            result.Add(new SwaggerParameterDescriptor
            {
                Name        = GetString(p, "name") ?? "param",
                In          = inVal,
                Type        = type,
                Description = GetString(p, "description"),
                Required    = p.TryGetProperty("required", out var req) && req.GetBoolean()
            });
        }

        return result;
    }

    // ── Body ──────────────────────────────────────────────────────────────────

    private static SwaggerBodyDescriptor? ParseBody(JsonElement operation, JsonElement root)
    {
        if (!operation.TryGetProperty("requestBody", out var rb)) return null;

        var required = rb.TryGetProperty("required", out var req) && req.GetBoolean();
        var desc     = GetString(rb, "description") ?? "Request body";

        if (!rb.TryGetProperty("content", out var content)) return null;
        if (!content.TryGetProperty("application/json", out var jsonContent)) return null;
        if (!jsonContent.TryGetProperty("schema", out var schema)) return null;

        // Follow $ref to get the actual schema with its properties
        var resolvedSchema = ResolveSchema(schema, root);

        return new SwaggerBodyDescriptor
        {
            Description = desc,
            Required    = required,
            Schema      = resolvedSchema
        };
    }

    /// <summary>
    /// Follows a JSON $ref pointer through the document.
    /// e.g. { "$ref": "#/components/schemas/AddRequest" }
    ///   → walks root → components → schemas → AddRequest
    /// </summary>
    private static JsonElement ResolveSchema(JsonElement schema, JsonElement root)
    {
        if (schema.ValueKind != JsonValueKind.Object) return schema;
        if (!schema.TryGetProperty("$ref", out var refProp)) return schema;

        var refPath = refProp.GetString();
        if (string.IsNullOrWhiteSpace(refPath)) return schema;

        var parts   = refPath.TrimStart('#', '/').Split('/');
        var current = root;

        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out var next)) return schema;
            current = next;
        }

        return current;
    }

    // ── Input Schema ──────────────────────────────────────────────────────────

    private static JsonSchema BuildInputSchema(
        List<SwaggerParameterDescriptor> parameters,
        SwaggerBodyDescriptor? body)
    {
        var props    = new Dictionary<string, JsonSchemaProperty>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            props[p.Name] = new JsonSchemaProperty { Type = p.Type, Description = p.Description };
            if (p.Required) required.Add(p.Name);
        }

        if (body?.Schema is { } bodySchema && bodySchema.ValueKind == JsonValueKind.Object)
        {
            // Try to expand the body schema's properties directly
            if (bodySchema.TryGetProperty("properties", out var bodyProps))
            {
                foreach (var prop in bodyProps.EnumerateObject())
                {
                    var propType = GetString(prop.Value, "type") ?? "string";

                    // KEY FIX: read description directly from the property element in the schema
                    // Swashbuckle writes [McpParam] descriptions here:
                    //   components.schemas.AddRequest.properties.a.description
                    var propDesc = GetString(prop.Value, "description");

                    props[prop.Name] = new JsonSchemaProperty
                    {
                        Type        = propType,
                        Description = propDesc
                    };
                }

                // Required list from the body schema
                if (bodySchema.TryGetProperty("required", out var reqArray))
                    foreach (var r in reqArray.EnumerateArray())
                    {
                        var rName = r.GetString();
                        if (!string.IsNullOrWhiteSpace(rName) && !required.Contains(rName))
                            required.Add(rName!);
                    }
                else if (body.Required)
                    foreach (var key in props.Keys)
                        if (!required.Contains(key)) required.Add(key);
            }
            else
            {
                // Schema has no properties (e.g. free-form object) — keep as single body param
                props["body"] = new JsonSchemaProperty { Type = "object", Description = body.Description };
                if (body.Required) required.Add("body");
            }
        }

        return new JsonSchema { Properties = props, Required = required };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}