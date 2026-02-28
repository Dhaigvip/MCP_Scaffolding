using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Swagger;

/// <summary>
/// Abstraction over tool loading strategies (Swagger JSON vs. in-process Palma API).
/// Implementations produce the shared <see cref="SwaggerToolDescriptor"/> format consumed
/// by <see cref="DynamicToolRegistry"/>.
/// </summary>
public interface IToolLoader
{
    Task<List<SwaggerToolDescriptor>> LoadAsync(CancellationToken ct = default);
}
