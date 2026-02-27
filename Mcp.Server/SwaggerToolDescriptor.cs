namespace Mcp.Swagger;

/// <summary>
/// Represents a single OpenAPI operation translated into an MCP tool descriptor.
/// Produced by SwaggerToolLoader at startup, consumed by DynamicToolRegistry.
/// </summary>
public sealed class SwaggerToolDescriptor
{
    /// <summary>Tool name sent to MCP clients e.g. "orders_get_by_id"</summary>
    public required string ToolName { get; init; }

    /// <summary>Human readable description taken from OpenAPI summary/description.</summary>
    public required string Description { get; init; }

    /// <summary>HTTP method: GET, POST, PUT, DELETE, PATCH</summary>
    public required string HttpMethod { get; init; }

    /// <summary>Path template e.g. /orders/{id}</summary>
    public required string PathTemplate { get; init; }

    /// <summary>Parameters (path, query, header). Body is a separate property.</summary>
    public List<SwaggerParameterDescriptor> Parameters { get; init; } = [];

    /// <summary>Body schema if the operation accepts a request body.</summary>
    public SwaggerBodyDescriptor? Body { get; init; }

    /// <summary>JSON Schema object describing the full input (built from Parameters + Body).</summary>
    public required JsonSchema InputSchema { get; init; }
}

public sealed class SwaggerParameterDescriptor
{
    public required string Name { get; init; }
    public required string In { get; init; }        // "path" | "query" | "header"
    public required string Type { get; init; }      // "string" | "integer" | "boolean" etc.
    public string? Description { get; init; }
    public bool Required { get; init; }
}

public sealed class SwaggerBodyDescriptor
{
    public required string Description { get; init; }
    public required bool Required { get; init; }
    // Raw JSON schema object for the body — passed through to MCP input schema
    public required System.Text.Json.JsonElement Schema { get; init; }
}

/// <summary>
/// Lightweight JSON Schema envelope for MCP tool InputSchema.
/// We build this manually so we control exactly what gets sent to the LLM client.
/// </summary>
public sealed class JsonSchema
{
    public string Type { get; init; } = "object";
    public Dictionary<string, JsonSchemaProperty> Properties { get; init; } = [];
    public List<string> Required { get; init; } = [];
}

public sealed class JsonSchemaProperty
{
    public required string Type { get; init; }
    public string? Description { get; init; }
}