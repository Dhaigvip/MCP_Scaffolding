using System.Collections.Concurrent;
using Mcp.Palma;
using Mcp.Palma.Contracts;

namespace Mcp.Agent;

/// <summary>
/// Thread-safe session store with automatic cleanup of idle sessions.
/// Registered as a singleton in DI.
/// </summary>
public sealed class SessionManager : IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly Timer _cleanupTimer;

    public SessionManager()
    {
        _cleanupTimer = new Timer(Cleanup, null, CleanupInterval, CleanupInterval);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public AgentSession Create(PalmaContext? palmaContext = null)
    {
        var session = new AgentSession { PalmaContext = palmaContext };
        _sessions[session.Id] = session;
        return session;
    }

    public AgentSession? Get(string id)
    {
        if (!_sessions.TryGetValue(id, out var session)) return null;
        session.LastActivity = DateTime.UtcNow;
        return session;
    }

    public bool Delete(string id)
    {
        if (!_sessions.TryRemove(id, out var session)) return false;
        session.AbortAllPendingHitl();
        return true;
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    private void Cleanup(object? state)
    {
        var cutoff = DateTime.UtcNow - IdleTimeout;

        foreach (var (id, session) in _sessions)
        {
            if (session.LastActivity < cutoff)
            {
                session.AbortAllPendingHitl();
                _sessions.TryRemove(id, out var _removed);
            }
        }
    }

    public void Dispose() => _cleanupTimer.Dispose();
}
