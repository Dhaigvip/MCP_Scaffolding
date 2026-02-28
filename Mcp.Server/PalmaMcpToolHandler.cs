using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Mcp.Palma;
using Mcp.Palma.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Mcp.Swagger;

/// <summary>
/// MCP protocol handler for the Palma in-process pipeline.
///
/// Unlike <see cref="DynamicMcpToolHandler"/> (Swagger — static registry), this
/// handler is fully dynamic: it calls <see cref="IPalmaEndpointSource.GetEndpoints"/>
/// on every <c>tools/list</c> request so the tool list is always version-aware and
/// session-scoped.  No startup loading, no DynamicToolRegistry.
///
/// PalmaContext is resolved per-request from HTTP headers:
///   X-Palma-Version, X-Palma-Org, X-Palma-Ms, X-Palma-Branch
/// with fallback to the configured defaults in <see cref="PalmaMcpOptions"/>.
/// </summary>
public sealed class PalmaMcpToolHandler
{
    private readonly IPalmaEndpointSource _source;
    private readonly IPalmaToolInvoker _invoker;
    private readonly IExposureService _exposure;
    private readonly IPolicyEngine _policy;
    private readonly GovernanceOptions _govOptions;
    private readonly PalmaMcpOptions _palmaOptions;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PalmaMcpToolHandler> _logger;

    public PalmaMcpToolHandler(
        IPalmaEndpointSource source,
        IPalmaToolInvoker invoker,
        IExposureService exposure,
        IPolicyEngine policy,
        GovernanceOptions govOptions,
        PalmaMcpOptions palmaOptions,
        ICorrelationIdAccessor correlationId,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PalmaMcpToolHandler> logger)
    {
        _source = source;
        _invoker = invoker;
        _exposure = exposure;
        _policy = policy;
        _govOptions = govOptions;
        _palmaOptions = palmaOptions;
        _correlationId = correlationId;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    // ── tools/list ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a version-aware tool list built fresh from <see cref="IPalmaEndpointSource"/>
    /// on every call — no cached registry involved.
    /// </summary>
    public ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken ct)
    {
        var palmaContext = GetPalmaContext();

        var contextParams = new HashSet<string>(
            _palmaOptions.ContextQueryParams,
            StringComparer.OrdinalIgnoreCase);

        var tools = _source
            .GetEndpoints(palmaContext.Version)
            .Where(e => _exposure.TryGetEnabledPolicy(PalmaToolBuilder.ToToolName(e.Uri), out _))
            .Select(e =>
            {
                var toolName = PalmaToolBuilder.ToToolName(e.Uri);
                var parameters = e.QueryParams
                    .Where(p => !contextParams.Contains(p.Name))
                    .ToList();

                return new Tool
                {
                    Name = toolName,
                    Description = e.Summary ?? e.Description ?? toolName,
                    InputSchema = BuildMcpSchema(parameters)
                };
            })
            .ToList();

        _logger.LogDebug("tools/list → {Count} tools for version {Version}",
            tools.Count, palmaContext.Version);

        return ValueTask.FromResult(new ListToolsResult { Tools = tools });
    }

    // ── tools/call ────────────────────────────────────────────────────────────

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken ct)
    {
        var toolName = context.Params?.Name
            ?? throw new McpException("Tool name is required.");

        // ── 1. Exposure + policy gate ─────────────────────────────────────
        if (!_exposure.TryGetEnabledPolicy(toolName, out var policy))
        {
            _logger.LogWarning("Tool {Name} is not exposed or disabled.", toolName);
            throw new McpException($"Tool not found: {toolName}");
        }

        var tenant = _govOptions.TenantResolver();
        _policy.EnforceToolPolicy(toolName, policy, _exposure.Current.GlobalMaxRisk, tenant);

        // ── 2. Resolve PalmaContext ───────────────────────────────────────
        var palmaContext = GetPalmaContext();

        // ── 3. Find matching endpoint URI ─────────────────────────────────
        var endpoint = _source
            .GetEndpoints(palmaContext.Version)
            .FirstOrDefault(e => PalmaToolBuilder.ToToolName(e.Uri) == toolName)
            ?? throw new McpException($"No endpoint found for tool '{toolName}' at version {palmaContext.Version}.");

        // ── 4. Extract LLM arguments (strip context params) ──────────────
        var arguments = (context.Params?.Arguments as IReadOnlyDictionary<string, JsonElement>)
            ?? new Dictionary<string, JsonElement>();

        var contextParamNames = new HashSet<string>(
            _palmaOptions.ContextQueryParams,
            StringComparer.OrdinalIgnoreCase);

        var additionalParams = arguments
            .Where(kv => !contextParamNames.Contains(kv.Key))
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ValueKind == JsonValueKind.String
                    ? kv.Value.GetString()!
                    : kv.Value.ToString());

        // ── 5. Invoke in-process ──────────────────────────────────────────
        var result = await _invoker.InvokeAsync(endpoint.Uri, palmaContext, additionalParams, ct);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result }]
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <see cref="PalmaContext"/> for the current request.
    /// Priority: X-Palma-* request headers → PalmaMcpOptions defaults.
    /// </summary>
    private PalmaContext GetPalmaContext()
    {
        var req = _httpContextAccessor.HttpContext?.Request;

        string? Header(string name) =>
            req?.Headers[name].FirstOrDefault();

        var version = Header("X-Palma-Version") ?? _palmaOptions.DefaultVersion
            ?? throw new McpException(
                "Palma version not specified. Set X-Palma-Version header or PalmaMcp:DefaultVersion in config.");

        var org  = Header("X-Palma-Org")    ?? _palmaOptions.DefaultOrg;
        var ms   = Header("X-Palma-Ms")     ?? _palmaOptions.DefaultMs;
        var branch = Header("X-Palma-Branch") ?? _palmaOptions.DefaultBranch;

        return new PalmaContext(version, org ?? string.Empty, ms, branch);
    }

    private static JsonElement BuildMcpSchema(IEnumerable<PalmaQueryParamInfo> parameters)
    {
        var props = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var p in parameters)
        {
            props[p.Name] = new Dictionary<string, string?>
            {
                ["type"] = p.Type,
                ["description"] = p.Description
            };
            if (p.Required) required.Add(p.Name);
        }

        var obj = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
        if (required.Count > 0) obj["required"] = required;

        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
