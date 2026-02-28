using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Palma.Contracts;
using Mcp.Swagger;

namespace Mcp.Palma;

/// <summary>
/// Adapts <see cref="IToolInvoker"/> (used by the MCP agent loop) to the
/// in-process <see cref="IPalmaToolInvoker"/> contract implemented by the host
/// Palma web API.  No HTTP — the call goes directly into the Palma pipeline.
/// </summary>
public sealed class PalmaToolInvoker : IToolInvoker
{
    private readonly IPalmaToolInvoker _inProcess;

    public PalmaToolInvoker(IPalmaToolInvoker inProcess)
    {
        _inProcess = inProcess;
    }

    public async Task<string> InvokeAsync(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        PalmaContext? palmaContext,
        string correlationId,
        CancellationToken ct)
    {
        if (palmaContext is null)
            throw new InvalidOperationException(
                $"PalmaContext is required to invoke Palma tool '{tool.ToolName}'. " +
                "Ensure the client passes version, org, ms, and branch at session creation.");

        // Convert LLM-supplied JSON args (query params only) to plain string dict
        var queryParams = tool.Parameters
            .Where(p => p.In == "query" && arguments.ContainsKey(p.Name))
            .ToDictionary(
                p => p.Name,
                p =>
                {
                    var val = arguments[p.Name];
                    return val.ValueKind == JsonValueKind.String
                        ? val.GetString()!
                        : val.ToString();
                });

        return await _inProcess.InvokeAsync(tool.PathTemplate, palmaContext, queryParams, ct);
    }
}
