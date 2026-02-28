using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Mcp.Agent;
using Mcp.Palma;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mcp.Http.Controllers;

/// <summary>
/// Agent API surface consumed by the React frontend.
///
/// Session management (both protocols):
///   POST   /api/agent/session      — create session → { sessionId }
///   DELETE /api/agent/session/{id} — delete session
///
/// ── WebSocket (bidirectional, preferred) ──────────────────────────────────────
///   GET  /api/agent/ws/{id}
///
///   Client → Server frames (JSON):
///     { "type": "chat",    "message": "user text" }
///     { "type": "approve", "callId": "...", "approved": true|false }
///
///   Server → Client frames (JSON):
///     AgentEvent stream (thinking / text_delta / hitl / tool_* / done / error)
///
/// ── SSE (one-way stream + separate approve call) ──────────────────────────────
///   POST /api/agent/chat    — SSE stream; body: { sessionId, message }
///   POST /api/agent/approve — resolve HITL; body: { sessionId, callId, approved }
/// </summary>
[ApiController]
[Route("api/agent")]
public sealed class AgentController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly AgentService _agentService;
    private readonly SessionManager _sessions;

    public AgentController(AgentService agentService, SessionManager sessions)
    {
        _agentService = agentService;
        _sessions = sessions;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Session management
    // ═══════════════════════════════════════════════════════════════════════════

    [HttpPost("session")]
    public IActionResult CreateSession([FromBody] CreateSessionRequest? request = null)
    {
        var session = _sessions.Create(request?.PalmaContext);
        return Ok(new { sessionId = session.Id });
    }

    [HttpDelete("session/{id}")]
    public IActionResult DeleteSession(string id)
    {
        _sessions.Delete(id);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WebSocket  —  GET /api/agent/ws/{id}
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full-duplex WebSocket for the lifetime of a session.
    ///
    /// • Multiple turns on one connection — no reconnect needed.
    /// • HITL approval travels on the same socket (no extra HTTP round-trip).
    /// • An unbounded Channel serialises writes from concurrent agent turns so
    ///   the WebSocket sender never has a data race.
    /// </summary>
    [HttpGet("ws/{id}")]
    public async Task AgentWebSocket(string id, CancellationToken ct)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Expected a WebSocket upgrade request.", ct);
            return;
        }

        var session = _sessions.Get(id);
        if (session is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

        var outChannel = Channel.CreateUnbounded<AgentEvent>(
            new UnboundedChannelOptions { SingleReader = true });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sendTask = SendLoopAsync(ws, outChannel.Reader, cts.Token);

        try
        {
            var buf = new byte[8 * 1024];

            while (ws.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult wsResult;
                try { wsResult = await ws.ReceiveAsync(buf, cts.Token); }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException) { break; }

                if (wsResult.MessageType == WebSocketMessageType.Close)
                {
                    if (ws.State == WebSocketState.CloseReceived)
                        await ws.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }

                WsClientMessage? msg;
                try
                {
                    var json = Encoding.UTF8.GetString(buf, 0, wsResult.Count);
                    msg = JsonSerializer.Deserialize<WsClientMessage>(json, JsonOptions);
                }
                catch { continue; }

                if (msg is null) continue;

                switch (msg.Type)
                {
                    case "chat" when !string.IsNullOrWhiteSpace(msg.Message):
                        _ = RunAgentTurnAsync(id, msg.Message!, outChannel.Writer, cts.Token);
                        break;

                    case "approve":
                        session.ResolveHitl(msg.CallId ?? "", msg.Approved ?? false);
                        break;
                }
            }
        }
        finally
        {
            cts.Cancel();
            outChannel.Writer.TryComplete();
        }

        await sendTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SSE  —  POST /api/agent/chat
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sends a user message and streams the agent response as Server-Sent Events.
    ///
    /// The SSE connection remains open while the agent is paused waiting for HITL
    /// approval.  Resume by calling POST /api/agent/approve on a separate request.
    ///
    /// Each event is emitted as:  data: {json}\n\n
    /// </summary>
    [HttpPost("chat")]
    public async Task Chat([FromBody] SseChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.StartAsync(ct);

        try
        {
            await foreach (var evt in _agentService.RunTurnAsync(
                               request.SessionId, request.Message, ct))
            {
                await WriteSseEventAsync(evt, ct);
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        catch (Exception ex)
        {
            await WriteSseEventAsync(new ErrorAgentEvent(ex.Message), ct);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HITL approval  —  POST /api/agent/approve  (used by SSE clients)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves a pending HITL approval from an SSE client.
    /// WebSocket clients send approval frames directly on the socket instead.
    /// </summary>
    [HttpPost("approve")]
    public IActionResult Approve([FromBody] ApprovalRequest request)
    {
        var session = _sessions.Get(request.SessionId);
        if (session is null)
            return NotFound(new { error = "Session not found." });

        var resolved = session.ResolveHitl(request.CallId, request.Approved);
        if (!resolved)
            return NotFound(new { error = "No pending approval for that callId." });

        return Ok(new { resolved = true });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Shared helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Runs one agent turn and writes every event into the channel.</summary>
    private async Task RunAgentTurnAsync(
        string sessionId,
        string message,
        ChannelWriter<AgentEvent> writer,
        CancellationToken ct)
    {
        try
        {
            await foreach (var evt in _agentService.RunTurnAsync(sessionId, message, ct))
                await writer.WriteAsync(evt, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            try { await writer.WriteAsync(new ErrorAgentEvent(ex.Message), ct); }
            catch { }
        }
    }

    /// <summary>Drains the channel and sends each event as a WebSocket text frame.</summary>
    /// <remarks>
    /// The inner catch handles individual send failures; the outer try/catch handles
    /// OperationCanceledException thrown by ReadAllAsync when the linked CTS is
    /// cancelled (e.g. client toggles protocol, navigates away, or disconnects).
    /// Without the outer catch the exception propagates through AgentWebSocket's
    /// "await sendTask" and surfaces as an unhandled server exception.
    /// </remarks>
    private static async Task SendLoopAsync(
        WebSocket ws,
        ChannelReader<AgentEvent> reader,
        CancellationToken ct)
    {
        try
        {
            await foreach (var evt in reader.ReadAllAsync(ct))
            {
                if (ws.State != WebSocketState.Open) break;
                try
                {
                    var json  = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
                }
                catch (WebSocketException)         { break; }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { /* normal: socket closed or protocol toggled */ }
    }

    /// <summary>Writes one AgentEvent as an SSE data line and flushes.</summary>
    private async Task WriteSseEventAsync(AgentEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}

// ─── Request / message models ─────────────────────────────────────────────────

/// <summary>Optional body for POST /api/agent/session — omit when using the Swagger pipeline.</summary>
public sealed record CreateSessionRequest(PalmaContext? PalmaContext = null);

/// <summary>Body for POST /api/agent/chat (SSE).</summary>
public sealed record SseChatRequest(string SessionId, string Message);

/// <summary>Body for POST /api/agent/approve (SSE HITL).</summary>
public sealed record ApprovalRequest(string SessionId, string CallId, bool Approved);

/// <summary>WebSocket frame sent from the React client.</summary>
public sealed record WsClientMessage(
    string Type,       // "chat" | "approve"
    string? Message,    // for "chat"
    string? CallId,     // for "approve"
    bool? Approved);  // for "approve"
