namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// The <c>agent</c> settings section: OpenAI-compatible API endpoint, model as <c>vendor/model</c> and its parameters.
/// The endpoint can point to another service or a local Ollama without code changes. The key lives in the secret
/// store, not here. Empty parameters mean model defaults.
/// </summary>
public sealed class AgentOptions
{
    public const string Section = "agent";

    /// <summary>AITUNNEL is the default and only selectable service (ADR 0021).</summary>
    public const string DefaultEndpoint = AgentServices.AitunnelEndpoint;

    /// <summary>Per the "Shop" benchmark (<c>tests/CodeEditor.Agent.Eval</c>): solves everything, cheapest per solved task.</summary>
    public const string DefaultModel = "openai/gpt-6-luna";

    public string Endpoint { get; set; } = DefaultEndpoint;

    public string Model { get; set; } = DefaultModel;

    /// <summary>API protocol: by model, Chat Completions or Responses.</summary>
    public AgentApi Api { get; set; } = AgentApi.Auto;

    /// <summary>What the agent may do: everything, only answer or only plan (ADR 0010).</summary>
    public AgentMode Mode { get; set; } = AgentMode.Agent;

    /// <summary>
    /// Approve each edit via a card or accept edits at once (deletion and commands always ask). Defaults to at once, as
    /// in Cursor and Copilot: edits show in the file, where each can be accepted or rejected.
    /// </summary>
    public AgentApprovals Approvals { get; set; } = AgentApprovals.Auto;

    /// <summary>
    /// Helper model for memory notes after a turn and for compaction summaries when the conversation request cannot be
    /// replayed (normally the conversation model writes the summary from cache, ADR 0023); <c>null</c> for the agent
    /// model. A fast, cheap model is fine.
    /// </summary>
    public string? HelperModel { get; set; }

    /// <summary>Model of the <c>explore</c> scout (ADR 0012); <c>null</c> for the agent model, like Claude Code's Explore.</summary>
    public string? ExplorerModel { get; set; }

    /// <summary>Scout reasoning effort: search and reading need little deliberation.</summary>
    public AgentReasoningEffort ExplorerReasoningEffort { get; set; } = AgentReasoningEffort.Low;

    /// <summary>
    /// Advisor and critic model for Deep mode (ADR 0012); <c>null</c> for the agent model. Best is a strong model from
    /// another vendor: a critic weaker than the executor hurts, and the same model shares the same blind spots.
    /// </summary>
    public string? AdvisorModel { get; set; }

    /// <summary>Advisor reasoning effort: it speaks rarely and briefly, so it can think longer.</summary>
    public AgentReasoningEffort AdvisorReasoningEffort { get; set; } = AgentReasoningEffort.High;

    /// <summary>Models for quick selection in chat; any other can be typed into the picker.</summary>
    public List<string> Models { get; set; } = [];

    /// <summary>Response randomness 0–2; <c>null</c> for the model default.</summary>
    public double? Temperature { get; set; }

    /// <summary>Response length limit in tokens; <c>null</c> for the model default.</summary>
    public int? MaxOutputTokens { get; set; }

    public AgentReasoningEffort ReasoningEffort { get; set; }

    /// <summary>Model context window in tokens for the indicator; <c>null</c> to look it up among known models.</summary>
    public int? ContextWindow { get; set; }

    /// <summary>
    /// Chat context limit in tokens (ADR 0012): beyond it old tool results are collapsed and the early conversation is
    /// compacted into a summary; <c>null</c> for 200 000. Never exceeds the model window.
    /// </summary>
    public int? ContextLimit { get; set; }

    /// <summary>Update folder memory after a turn with the helper model (ADR 0012); disable with <c>agent.autoMemory: false</c>.</summary>
    public bool AutoMemory { get; set; } = true;

    /// <summary>
    /// Model traffic log: raw request bodies and responses in <c>logs/model-traffic</c>, without headers or key
    /// (<see cref="TrafficLogPolicy"/>). For investigating service responses; the log contains code and chat questions.
    /// </summary>
    public bool TrafficLog { get; set; }

    /// <summary>
    /// One-hour Claude cache warm-up while the chat waits longer than 4 minutes (<see cref="CacheWarmupChatClient"/>,
    /// ADR 0022); disable with <c>agent.cacheWarmup: false</c>.
    /// </summary>
    public bool CacheWarmup { get; set; } = true;

    /// <summary>
    /// Web search model (<c>web_search</c>, <see cref="WebSearchClient"/>): a fast, cheap one, as it only searches and
    /// summarizes.
    /// </summary>
    public string WebSearchModel { get; set; } = DefaultWebSearchModel;

    public const string DefaultWebSearchModel = "openai/gpt-6-luna";

    /// <summary>
    /// Sites whose pages the agent reads without asking (<c>web_fetch</c>), in addition to the default documentation
    /// sites (<see cref="WebApprovals.DefaultHosts"/>); "Always allow" on a card adds the site here.
    /// </summary>
    public List<string> WebHosts { get; set; } = [];
}
