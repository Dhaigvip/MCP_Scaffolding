using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace MathApi.Mcp;

/// <summary>
/// Swashbuckle IOperationFilter — reads [McpTool] from the action method
/// and writes its description into the OpenAPI operation summary.
/// This is what SwaggerToolLoader reads as the tool description.
/// </summary>
public sealed class McpToolOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attr = context.MethodInfo.GetCustomAttribute<McpToolAttribute>();
        if (attr is null) return;

        // Overwrite summary with the MCP description
        // SwaggerToolLoader reads "summary" first, then "description"
        operation.Summary     = attr.Description;
        operation.Description = attr.Description;
    }
}

/// <summary>
/// Swashbuckle ISchemaFilter — reads [McpParam] from all properties on a request type
/// and writes their descriptions into the OpenAPI schema's property map.
///
/// Runs at the TYPE level (not member level) so it correctly handles $ref resolution.
/// For each property on the class that has [McpParam], it finds the matching OpenAPI
/// property in schema.Properties and sets its description.
/// </summary>
public sealed class McpParamSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Only run on class types that have properties — skip primitives, enums, etc.
        if (context.Type is null) return;
        if (!schema.Properties.Any()) return;

        // Walk every CLR property on the type looking for [McpParam]
        foreach (var prop in context.Type.GetProperties())
        {
            var attr = prop.GetCustomAttribute<McpParamAttribute>();
            if (attr is null) continue;

            // Swagger lowercases the first letter of property names by default (camelCase)
            // so we try both the original name and the camelCase version
            var swaggerKey = schema.Properties.Keys
                .FirstOrDefault(k => k.Equals(prop.Name, StringComparison.OrdinalIgnoreCase));

            if (swaggerKey is null) continue;

            schema.Properties[swaggerKey].Description = attr.Description;
        }
    }
}