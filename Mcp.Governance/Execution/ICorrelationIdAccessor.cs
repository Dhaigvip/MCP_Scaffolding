namespace Mcp.Governance.Execution;

/// <summary>
/// Abstracts correlation ID resolution.
/// Replaces the raw Func&lt;string&gt; delegate that was unresolvable from DI.
/// </summary>
public interface ICorrelationIdAccessor
{
    string CorrelationId { get; }
}