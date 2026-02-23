using Microsoft.AspNetCore.Http;

namespace Mcp.Governance.Execution;

/// <summary>
/// Reads correlation ID from the request header, falling back to a new GUID.
/// Header name is configurable — defaults to "X-Correlation-Id".
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