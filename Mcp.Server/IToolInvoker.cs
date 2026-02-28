using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Palma;
using Mcp.Palma.Contracts;

namespace Mcp.Swagger;

/// <summary>
/// Abstraction over tool invocation strategies (Swagger HTTP vs. in-process Palma API).
/// Implementations receive a resolved tool descriptor and the LLM-supplied arguments,
/// then forward the call to the underlying API and return a result string for the LLM.
/// </summary>
public interface IToolInvoker
{
    /// <param name="tool">Tool descriptor resolved from the registry.</param>
    /// <param name="arguments">Arguments supplied by the LLM (already validated).</param>
    /// <param name="palmaContext">
    ///   Session-level Palma context (version, org, ms, branch).
    ///   <c>null</c> when not using the Palma loader — implementors may ignore it.
    /// </param>
    /// <param name="correlationId">Forwarded as X-Correlation-Id for end-to-end tracing.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> InvokeAsync(
        SwaggerToolDescriptor tool,
        IReadOnlyDictionary<string, JsonElement> arguments,
        PalmaContext? palmaContext,
        string correlationId,
        CancellationToken ct);
}
