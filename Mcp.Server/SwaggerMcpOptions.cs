namespace Mcp.Swagger;

/// <summary>
/// Configuration for the Swagger → MCP auto-exposure pipeline.
/// Bound from appsettings.json under "SwaggerMcp".
/// </summary>
public sealed class SwaggerMcpOptions
{
    /// <summary>
    /// Full URL to your WebAPI's swagger.json.
    /// Examples:
    ///   "https://localhost:7001/swagger/v1/swagger.json"   (external)
    ///   "/swagger/v1/swagger.json"                         (when embedded in host)
    /// </summary>
    public required string SwaggerUrl { get; init; }

    /// <summary>
    /// Base URL of your WebAPI — prepended to all tool HTTP calls.
    /// Example: "https://localhost:7001"
    /// When embedded in the host WebAPI, set this to the host's own base URL.
    /// </summary>
    public required string ApiBaseUrl { get; init; }
}