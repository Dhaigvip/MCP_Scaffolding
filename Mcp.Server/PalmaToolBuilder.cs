using Mcp.Palma.Contracts;
using Mcp.Swagger;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Mcp.Palma;

/// <summary>
/// Shared helper that converts <see cref="PalmaEndpointInfo"/> records into
/// <see cref="SwaggerToolDescriptor"/> objects consumed by the MCP registry.
/// Used by both <see cref="PalmaToolLoader"/> (in-process) and
/// <see cref="PalmaRemoteToolLoader"/> (HTTP).
/// </summary>
public static class PalmaToolBuilder
{
    public static List<SwaggerToolDescriptor> Build(
        IEnumerable<PalmaEndpointInfo> endpoints,
        IReadOnlySet<string> contextParams,
        ILogger logger)
    {
        var tools = new List<SwaggerToolDescriptor>();

        foreach (var endpoint in endpoints)
        {
            var toolName = ToToolName(endpoint.Uri);
            var description = endpoint.Summary ?? endpoint.Description ?? toolName;

            // Strip context params — LLM never supplies org/ms/branch; invoker injects them
            var parameters = endpoint.QueryParams
                .Where(p => !contextParams.Contains(p.Name))
                .Select(p => new SwaggerParameterDescriptor
                {
                    Name = p.Name,
                    In = "query",
                    Type = p.Type,
                    Description = p.Description,
                    Required = p.Required
                })
                .ToList();

            var schema = BuildSchema(parameters);

            tools.Add(new SwaggerToolDescriptor
            {
                ToolName = toolName,
                Description = description,
                HttpMethod = "POST",        // Palma accepts POST for all operations
                PathTemplate = endpoint.Uri,  // e.g. "GET/modules"
                Parameters = parameters,
                Body = null,
                InputSchema = schema
            });

            logger.LogInformation(
                "Registered Palma tool [{Tool}] ← {Uri} | {Count} input props",
                toolName, endpoint.Uri, schema.Properties.Count);
        }

        logger.LogInformation("Loaded {Count} Palma tools.", tools.Count);
        return tools;
    }

    /// <summary>"GET/modules" → "get_modules"</summary>
    public static string ToToolName(string uri) =>
        uri.ToLowerInvariant().Replace('/', '_').Trim('_');

    private static JsonSchema BuildSchema(List<SwaggerParameterDescriptor> parameters)
    {
        var props = new Dictionary<string, JsonSchemaProperty>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            props[p.Name] = new JsonSchemaProperty { Type = p.Type, Description = p.Description };
            if (p.Required) required.Add(p.Name);
        }

        return new JsonSchema { Properties = props, Required = required };
    }
}
