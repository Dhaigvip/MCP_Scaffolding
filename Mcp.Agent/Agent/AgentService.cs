using System.Runtime.CompilerServices;
using System.Text.Json;
using Mcp.Agent.ModelAbstraction;
using Mcp.Agent.ToolSource;
using Mcp.Governance.Execution;
using Mcp.Governance.Exposure;
using Mcp.Governance.Policy;
using Mcp.Swagger;

namespace Mcp.Agent;

/// <summary>
/// Core agent loop — provider-neutral.
///
/// Design decisions:
/// • Uses IAgentModelRouter to pick the right LLM per session (Anthropic, OpenAI, …).
/// • Tool list and descriptors come from IAgentToolSource, which is either:
///     - RegistryAgentToolSource (Swagger mode — static registry, loaded at startup)
///     - PalmaAgentToolSource   (Palma mode  — dynamic, version-aware per session)
/// • Risk levels come from ExposureManifest — admin-controlled, not keyword guessing.
/// • Returns IAsyncEnumerable&lt;AgentEvent&gt; so the controller can stream each event as SSE.
/// • HITL: awaits AgentSession.WaitForApproval(callId), suspending the enumerable
///   until POST /api/agent/approve resolves the TaskCompletionSource.
/// </summary>
public sealed class AgentService
{
    private const string SystemPrompt = """
        You are an AI assistant that operates exclusively through the REST API tools loaded from a Swagger/OpenAPI specification.

        ## Critical constraint — tools only
        You MUST NOT answer questions or perform tasks using your general training knowledge.
        Every response must be grounded in a tool call. If no available tool can fulfill the user's
        request, you MUST reply with a clear message such as:
          "I don't have a tool that can do that. The available operations are: <list tool names briefly>."
        Do not attempt to answer from memory, make up data, or provide workarounds using general knowledge.

        ## Personality
        - Be conversational and transparent about what you are doing and why
        - Before calling any tool, briefly explain your intent to the user
        - After tool results, summarize what you found or did in plain language
        - If a tool fails, explain what went wrong and suggest alternatives
        - Never batch multiple destructive operations in one response without pausing for user feedback

        ## Approach
        1. Identify which tool(s), if any, can fulfil the user's goal
        2. If no tool matches → immediately inform the user (do not guess or hallucinate)
        3. If a tool matches → briefly state your plan, then execute one logical step at a time
        4. Report results clearly after each step
        5. Ask for clarification when the request is ambiguous
        """;

    private readonly IAgentModelRouter _router;
    private readonly IAgentToolSource _toolSource;
    private readonly IToolInvoker _invoker;
    private readonly IExposureService _exposure;
    private readonly ICorrelationIdAccessor _correlationId;
    private readonly SessionManager _sessions;

    public AgentService(
        IAgentModelRouter router,
        IAgentToolSource toolSource,
        IToolInvoker invoker,
        IExposureService exposure,
        ICorrelationIdAccessor correlationId,
        SessionManager sessions)
    {
        _router = router;
        _toolSource = toolSource;
        _invoker = invoker;
        _exposure = exposure;
        _correlationId = correlationId;
        _sessions = sessions;
    }

    // ─── Public entry point ───────────────────────────────────────────────────

    public async IAsyncEnumerable<AgentEvent> RunTurnAsync(
        string sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var session = _sessions.Get(sessionId);
        if (session is null)
        {
            yield return new ErrorAgentEvent("Session not found. Please refresh the page.");
            yield break;
        }

        session.History.Add(new AgentMessage
        {
            Role = AgentRole.User,
            Parts = [AgentMessagePart.TextPart(userMessage)]
        });

        // Resolved once on first turn, then reused for the session lifetime.
        // Palma mode: version-aware (reads IPalmaEndpointSource for session.PalmaContext.Version).
        // Swagger mode: static registry populated at startup.
        var tools = session.CachedTools ??= _toolSource.GetTools(session);

        if (tools.Count == 0)
        {
            yield return new ErrorAgentEvent(
                "No API tools are currently available. " +
                "Please ensure the API specification is loaded and try again.");
            yield return new DoneAgentEvent();
            yield break;
        }

        while (!ct.IsCancellationRequested)
        {
            // ── Call the LLM ──────────────────────────────────────────────────
            AgentModelResponse? modelResponse = null;
            AgentEvent? callError = null;

            try
            {
                var modelRequest = new AgentModelRequest
                {
                    SystemPrompt = SystemPrompt,
                    Tools = tools,
                    Messages = session.History,
                    MaxTokens = 16_000,
                    Model = session.ModelName
                };

                var model = _router.Resolve(session, modelRequest);
                modelResponse = await model.GenerateAsync(modelRequest, ct);
            }
            catch (OperationCanceledException) { callError = new ErrorAgentEvent("Request cancelled."); }
            catch (Exception ex) { callError = new ErrorAgentEvent($"Model error: {ex.Message}"); }

            if (callError is not null) { yield return callError; yield break; }

            // ── Stream content to the client ──────────────────────────────────
            foreach (var part in modelResponse!.Parts)
            {
                if (part.Type == AgentPartType.Thinking && !string.IsNullOrWhiteSpace(part.Thinking))
                {
                    yield return new ThinkingAgentEvent(part.Thinking);
                    yield return new StatusAgentEvent("💭 Claude thought through the problem…");
                }
                else if (part.Type == AgentPartType.Text && !string.IsNullOrWhiteSpace(part.Text))
                {
                    foreach (var chunk in SplitIntoChunks(part.Text, chunkSize: 16))
                        yield return new TextDeltaAgentEvent(chunk);

                    yield return new TextEndAgentEvent();
                }
            }

            session.History.Add(new AgentMessage
            {
                Role = AgentRole.Assistant,
                Parts = modelResponse.Parts
                    .Where(p => p.Type != AgentPartType.Thinking)
                    .ToList()
            });

            if (!modelResponse.HasToolCalls) break;

            // ── Process tool calls ────────────────────────────────────────────
            var toolResultParts = new List<AgentMessagePart>();

            foreach (var part in modelResponse.Parts)
            {
                if (part.Type != AgentPartType.ToolCall) continue;

                var toolName = part.ToolName ?? "";
                var callId = part.ToolCallId ?? "";
                var input = part.ToolInput ?? new Dictionary<string, JsonElement>();
                var riskInfo = GetRiskInfo(toolName);

                if (riskInfo.RequiresApproval)
                {
                    yield return new HitlAgentEvent(callId, toolName, input, riskInfo);

                    bool approved;
                    AgentEvent? hitlError = null;
                    try { approved = await session.WaitForApproval(callId, ct); }
                    catch (OperationCanceledException)
                    {
                        hitlError = new ErrorAgentEvent("Request cancelled during approval wait.");
                        approved = false;
                    }

                    if (hitlError is not null) { yield return hitlError; yield break; }

                    if (!approved)
                    {
                        yield return new ToolSkippedAgentEvent(toolName);
                        toolResultParts.Add(AgentMessagePart.ToolResultPart(
                            callId, "User declined this operation.", isError: true));
                        continue;
                    }
                }
                else
                {
                    yield return new ToolAutoAgentEvent(toolName, input);
                }

                // ── Execute tool ──────────────────────────────────────────────
                string resultJson;
                bool isError;

                try
                {
                    var descriptor = _toolSource.GetDescriptor(toolName, session)
                        ?? throw new InvalidOperationException($"Tool '{toolName}' not found.");

                    resultJson = await _invoker.InvokeAsync(
                        descriptor, input, session.PalmaContext, _correlationId.CorrelationId, ct);
                    isError = false;
                }
                catch (Exception ex)
                {
                    resultJson = $"Tool execution error: {ex.Message}";
                    isError = true;
                }

                yield return new ToolResultAgentEvent(toolName, resultJson, isError);
                toolResultParts.Add(AgentMessagePart.ToolResultPart(callId, resultJson, isError));
            }

            session.History.Add(new AgentMessage
            {
                Role = AgentRole.User,
                Parts = toolResultParts
            });
        }

        yield return new DoneAgentEvent();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private RiskInfo GetRiskInfo(string toolName)
    {
        if (_exposure.TryGetEnabledPolicy(toolName, out var policy))
            return RiskInfo.FromRiskLevel(policy.Risk);

        return RiskInfo.FromRiskLevel(RiskLevel.Medium);
    }

    private static IEnumerable<string> SplitIntoChunks(string text, int chunkSize)
    {
        for (var i = 0; i < text.Length; i += chunkSize)
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
    }
}
