using Mcp.Governance.Execution;
using Microsoft.AspNetCore.Http;

namespace Mcp.Http;

/// <summary>
/// Reads the correlation ID from the incoming HTTP request header,
/// falling back to a new GUID if the header is absent.
/// Header name defaults to "X-Correlation-Id".
/// </summary>
public sealed class HttpContextCorrelationIdAccessor : ICorrelationIdAccessor
{
    public const string DefaultHeader = "X-Correlation-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _headerName;

    public HttpContextCorrelationIdAccessor(
        IHttpContextAccessor httpContextAccessor,
        string headerName = DefaultHeader)
    {
        _httpContextAccessor = httpContextAccessor;
        _headerName = headerName;
    }

    public string CorrelationId
    {
        get
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx is not null &&
                ctx.Request.Headers.TryGetValue(_headerName, out var values) &&
                !string.IsNullOrWhiteSpace(values))
            {
                return values.ToString();
            }

            return Guid.NewGuid().ToString("N");
        }
    }
}
