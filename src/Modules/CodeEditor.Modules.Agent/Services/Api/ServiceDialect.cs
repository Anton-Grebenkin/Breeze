namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// What a service accepts beyond the standard OpenAI-compatible request (ADR 0021). Model specifics live in the model
/// profile (<see cref="ModelProfiles"/>) and dedicated field classes; the dialect decides whether a field may be sent to
/// this service: a strict service answers 400 to an unknown field (Provod), a lenient one silently ignores it. Responses
/// are parsed leniently for every service, so the dialect does not cover reasoning field formats
/// (<see cref="ReasoningFieldPolicy"/>).
/// </summary>
public sealed record ServiceDialect
{
    /// <summary>Protocol for OpenAI models: Responses API or Chat Completions.</summary>
    public AgentApi OpenAIProtocol { get; init; } = AgentApi.Responses;

    /// <summary>
    /// Responses without stored output (<c>store: false</c>) and with encrypted reasoning between steps (<c>include</c>);
    /// otherwise past reasoning is stripped from history (<see cref="ResponsesReasoning"/>).
    /// </summary>
    public bool EncryptedReasoning { get; init; }

    /// <summary>Conversation cache key <c>prompt_cache_key</c> in Responses (<see cref="CacheKeyField"/>).</summary>
    public bool PromptCacheKey { get; init; }

    /// <summary>GPT reply phases (<c>phase</c>) in Responses history (<see cref="ResponsesPhases"/>).</summary>
    public bool ReplyPhases { get; init; }

    /// <summary><c>cache_control</c> marks in Claude messages (<see cref="AnthropicCache"/>).</summary>
    public bool CacheControlMarks { get; init; }

    /// <summary>The <c>thinking</c> field for Claude reasoning (<see cref="AnthropicThinking"/>).</summary>
    public bool ThinkingField { get; init; }

    /// <summary>Claude reasoning depth via <c>reasoning_effort</c>; without it Claude does not reason.</summary>
    public bool ClaudeReasoningEffort { get; init; }

    /// <summary>Pins Grok to the server holding its cache: the <c>session_id</c> field and the <c>x-grok-conv-id</c> header.</summary>
    public bool GrokSessionKey { get; init; }

    /// <summary>Grok's <c>reasoning_effort</c>: Grok 4 always reasons, and a strict service answers the field with 400.</summary>
    public bool GrokReasoningEffort { get; init; }

    /// <summary>
    /// Chat Completions response limit as legacy <c>max_tokens</c> (<see cref="CompletionsFields"/>): on Provod, Grok,
    /// DeepSeek and MiMo reject <c>max_completion_tokens</c>, while every model accepts <c>max_tokens</c>.
    /// </summary>
    public bool LegacyMaxTokens { get; init; }

    /// <summary>
    /// Claude via the native Anthropic Messages API (<see cref="AnthropicMessagesChatClient"/>) instead of translation to
    /// the OpenAI format: on Provod the translation corrupts call arguments when reasoning.
    /// </summary>
    public bool ClaudeMessages { get; init; }

    /// <summary>
    /// Model id without the vendor: AITUNNEL expects <c>gpt-6-luna</c> and forwards an id with a slash to OpenRouter as is,
    /// bypassing its own catalog. The full id is kept in the picker and profiles; only the request uses the short one.
    /// </summary>
    public bool ShortModelIds { get; init; }

    /// <summary>The proxy substitutes a placeholder for an empty reply (<see cref="ProxyPlaceholderChatClient"/>).</summary>
    public bool EmptyReplyPlaceholder { get; init; }

    /// <summary>
    /// Web search via the service's server tool (<c>aitunnel:web_search</c> in Chat Completions); backs the agent's
    /// <c>web_search</c> tool (<see cref="WebSearchClient"/>).
    /// </summary>
    public bool WebSearchTool { get; init; }

    /// <summary>
    /// OpenAI API: its own Responses fields, but no proxy extensions for Claude and Grok. Also used for a user endpoint
    /// outside <see cref="AgentServices"/>, about which nothing is known.
    /// </summary>
    public static ServiceDialect Standard { get; } = new()
    {
        EncryptedReasoning = true,
        PromptCacheKey = true,
        ReplyPhases = true,
        ClaudeReasoningEffort = true,
        GrokReasoningEffort = true,
    };
}
