namespace Mcp.Tooling;

/// <summary>
/// Compile-time constants for tool name identifiers.
/// These must match the keys in mcp_exposure.json exactly.
/// Using constants prevents silent mismatches between handler registration
/// and the exposure manifest that would cause tool_not_exposed at runtime.
/// </summary>
public static class ToolNames
{
    public const string HelloSay = "hello.say";

    // Add new tool names here as you onboard more APIs.
    // Example:
    // public const string OrdersGet   = "orders.get";
    // public const string OrdersCreate = "orders.create";
}
