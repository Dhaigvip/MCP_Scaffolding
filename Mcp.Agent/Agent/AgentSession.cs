
using Mcp.Agent.ModelAbstraction;
using Mcp.Palma;

namespace Mcp.Agent;

public sealed class AgentSession
{
    public string? ModelProvider { get; set; } // "anthropic" | "openai" | "gemini"
    public string? ModelName { get; set; } // optional model override per session

    /// <summary>
    /// Palma context supplied at session creation (version, org, ms, branch).
    /// Null when using the Swagger loader — injected into every Palma tool call.
    /// </summary>
    public PalmaContext? PalmaContext { get; set; }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public List<AgentMessage> History { get; } = new();

    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingApprovals = new();

    public bool ResolveHitl(string callId, bool approved)
    {
        lock (_pendingApprovals)
        {
            if (!_pendingApprovals.TryGetValue(callId, out var tcs)) return false;
            _pendingApprovals.Remove(callId);
            return tcs.TrySetResult(approved);
        }
    }

    public Task<bool> WaitForApproval(string callId, CancellationToken ct)
    {
        TaskCompletionSource<bool> tcs;
        lock (_pendingApprovals)
        {
            if (_pendingApprovals.TryGetValue(callId, out tcs!))
                return tcs.Task;

            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingApprovals[callId] = tcs;
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                lock (_pendingApprovals)
                {
                    if (_pendingApprovals.Remove(callId, out var existing))
                        existing.TrySetCanceled(ct);
                }
            });
        }

        return tcs.Task;
    }

    public void AbortAllPendingHitl()
    {
        lock (_pendingApprovals)
        {
            foreach (var kv in _pendingApprovals)
                kv.Value.TrySetCanceled();

            _pendingApprovals.Clear();
        }
    }
}