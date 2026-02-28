using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Swagger;

/// <summary>
/// Plugs into the MCP SDK's custom tool handler hooks.
/// Instead of one class per tool, this single handler:
///   1. Returns the dynamic tool list from DynamicToolRegistry (tools/list)
///   2. Dispatches any tools/call through governance then to SwaggerToolInvoker
///
/// Registered via .WithListToolsHandler() and .WithCallToolHandler() in Program.cs.
/// This is the key integration point — no attributes, no code gen, no per-tool classes.
/// </summary>
public sealed class DynamicMcpToolHandler
{
    private readonly DynamicToolRegistry _registry;
    private readonly IToolInvoker _invoker;
    private readonly IExposureService _exposure;
    private readonly IPolicyEngine _policy;
    private readonly GovernanceOptions _options;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly ILogger<DynamicMcpToolHandler> _logger;

    public DynamicMcpToolHandler(
        DynamicToolRegistry registry,
        IToolInvoker invoker,
        IExposureService exposure,
        IPolicyEngine policy,
        GovernanceOptions options,
        ICorrelationIdAccessor correlationId,
        ILogger<DynamicMcpToolHandler> logger)
    {
        _registry = registry;
        _invoker = invoker;
        _exposure = exposure;
        _policy = policy;
        _options = options;
        _correlationId = correlationId;
        _logger = logger;
    }

    /// <summary>
    /// Called by the MCP SDK when a client requests tools/list.
    /// Converts DynamicToolRegistry entries into MCP Tool protocol objects.
    /// </summary>
    public ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken ct)
    {
        var tools = _registry.All.Values.Select(d => new Tool
        {
            Name = d.ToolName,
            Description = d.Description,
            InputSchema = BuildMcpSchema(d.InputSchema)
        }).ToList();

        return ValueTask.FromResult(new ListToolsResult { Tools = tools });
    }

    /// <summary>
    /// Called by the MCP SDK when a client sends tools/call.
    /// Runs governance checks then delegates to SwaggerToolInvoker.
    /// </summary>
    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken ct)
    {
        var toolName = context.Params?.Name
            ?? throw new McpException("Tool name is required");

        // ── 1. Exposure + policy gate ─────────────────────────────────────
        if (!_exposure.TryGetEnabledPolicy(toolName, out var policy))
        {
            _logger.LogWarning("Tool {Name} is not exposed or disabled.", toolName);
            throw new McpException($"Tool not found: {toolName}");
        }

        var tenant = _options.TenantResolver();
        _policy.EnforceToolPolicy(toolName, policy, _exposure.Current.GlobalMaxRisk, tenant);

        // ── 2. Resolve descriptor ─────────────────────────────────────────
        if (!_registry.TryGet(toolName, out var descriptor))
            throw new McpException($"Tool not found in registry: {toolName}");

        // ── 3. Parse arguments ────────────────────────────────────────────
        var arguments = (context.Params?.Arguments
            as IReadOnlyDictionary<string, JsonElement>)
            ?? new Dictionary<string, JsonElement>();

        // ── 4. Forward to WebAPI ──────────────────────────────────────────
        var correlationId = _correlationId.CorrelationId;
        // MCP protocol callers don't carry a PalmaContext — pass null (Swagger pipeline)
        var result = await _invoker.InvokeAsync(descriptor, arguments, null, correlationId, ct);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result }]
        };
    }

    // ── Schema conversion ─────────────────────────────────────────────────

    private static JsonElement BuildMcpSchema(JsonSchema schema)
    {
        // Build a JSON Schema object the MCP protocol expects as InputSchema
        var obj = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = schema.Properties.ToDictionary(
                kv => kv.Key,
                kv => (object)new Dictionary<string, string?>
                {
                    ["type"] = kv.Value.Type,
                    ["description"] = kv.Value.Description
                }
            )
        };

        if (schema.Required.Count > 0)
            obj["required"] = schema.Required;

        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}