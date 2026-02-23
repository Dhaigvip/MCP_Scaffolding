namespace MathApi.Mcp;

/// <summary>
/// Decorates a WebAPI action method to provide a rich description
/// for when it is exposed as an MCP tool.
///
/// Usage:
///   [McpTool("Adds two numbers. Use when you need to sum values or calculate a total.")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// Description shown to the LLM in tools/list.
    /// Write this for the model, not a developer:
    ///   - Say WHEN to use it
    ///   - Give a concrete example
    ///   - Mention error cases
    /// </summary>
    public string Description { get; }

    public McpToolAttribute(string description)
    {
        Description = description;
    }
}

/// <summary>
/// Decorates a property on a request model to describe it as an MCP tool parameter.
/// Swashbuckle picks this up via a schema filter and injects it into swagger.json.
///
/// Usage:
///   [McpParam("The first number. Accepts any positive or negative decimal.")]
///   public double A { get; init; }
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class McpParamAttribute : Attribute
{
    /// <summary>
    /// Description shown to the LLM as the property description in inputSchema.
    /// Be specific about type constraints, valid ranges, and what the value means.
    /// </summary>
    public string Description { get; }

    public McpParamAttribute(string description)
    {
        Description = description;
    }
}