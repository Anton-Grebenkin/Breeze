namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>
/// Model services. Only AITUNNEL is selectable (<see cref="Selectable"/>): a proxy over OpenRouter that passes its
/// features through (Messages, Responses, <c>session_id</c>, one-hour Claude cache). ProxyAPI (direct to OpenAI and
/// Anthropic) and Provod (cheapest but unstable) stay in code: their dialects, keys and model lists work when the
/// endpoint is set in settings (<c>agent.endpoint</c>). Each service has its own key. An unknown endpoint uses the
/// ProxyAPI key and the standard dialect.
/// </summary>
public static class AgentServices
{
    public const string ProvodEndpoint = "https://api.provod.ai/v1";

    public const string ProxyApiEndpoint = "https://api.proxyapi.ru/v1";

    public const string AitunnelEndpoint = "https://api.aitunnel.ru/v1";

    /// <summary>
    /// ProxyAPI passes Responses fields through to OpenAI, and Claude and Grok extensions to LiteLLM and OpenRouter;
    /// verified by probes (ADR 0010, 0018).
    /// </summary>
    public static ServiceDialect ProxyApiDialect { get; } = ServiceDialect.Standard with
    {
        CacheControlMarks = true,
        ThinkingField = true,
        GrokSessionKey = true,
        EmptyReplyPlaceholder = true,
    };

    /// <summary>
    /// Provod is strict: an unknown field means 400. Its Responses API rejects <c>store</c>, <c>include</c>, encrypted
    /// reasoning and <c>prompt_cache_key</c>. Probe results:
    /// <list type="bullet">
    /// <item>GPT reply phases are accepted; GPT and Claude caching works without a key or marks;</item>
    /// <item>Claude's <c>thinking</c> and Grok's <c>max_completion_tokens</c> give 400 <c>unsupported_parameter</c>
    /// (DeepSeek and MiMo lack <c>max_completion_tokens</c> too); Grok's <c>reasoning_effort</c> gives 400
    /// <c>unsupported_parameter_combination</c>;</item>
    /// <item>with <c>reasoning_effort</c> Claude goes to Anthropic directly and the service loses spaces where call
    /// argument chunks join (<c>"b =="</c> + <c>"0 ? …"</c> → <c>b ==0</c>), so edits stop matching the file; without it
    /// Claude goes via Bedrock and does not reason. Hence Claude uses Anthropic Messages (<c>/v1/messages</c>), the way
    /// Provod connects Claude Code;</item>
    /// <item>Grok's <c>session_id</c> is accepted but does not reduce cache misses, so it is not sent.</item>
    /// </list>
    /// </summary>
    public static ServiceDialect ProvodDialect { get; } = new() { ReplyPhases = true, LegacyMaxTokens = true, ClaudeMessages = true };

    /// <summary>
    /// AITUNNEL sits on OpenRouter and passes through or ignores unknown fields. Model ids are short, from its catalog:
    /// an id with a slash would go to OpenRouter bypassing the catalog and its prices. Claude goes via Messages (signed
    /// reasoning, cache marks), the cache is pinned by <c>session_id</c>, and the response limit is <c>max_tokens</c>,
    /// which the service uses to reserve the request cost.
    /// </summary>
    public static ServiceDialect AitunnelDialect { get; } = ServiceDialect.Standard with
    {
        CacheControlMarks = true,
        ThinkingField = true,
        GrokSessionKey = true,
        GrokReasoningEffort = true,
        LegacyMaxTokens = true,
        ClaudeMessages = true,
        ShortModelIds = true,
        WebSearchTool = true,
    };

    public static AgentService ProxyApi { get; } = new("ProxyAPI", ProxyApiEndpoint, AgentSecrets.ApiKey, ModelCatalog.ProxyApiModels, ProxyApiDialect)
    {
        KeyPage = new Uri("https://proxyapi.ru/"),
    };

    public static AgentService Provod { get; } = new("Provod", ProvodEndpoint, AgentSecrets.ProvodApiKey, ModelCatalog.ProvodModels, ProvodDialect)
    {
        KeyPage = new Uri("https://provod.ai/"),
    };

    public static AgentService Aitunnel { get; } = new("AITUNNEL", AitunnelEndpoint, AgentSecrets.AitunnelApiKey, ModelCatalog.AitunnelModels, AitunnelDialect)
    {
        KeyPage = new Uri("https://aitunnel.ru/"),
    };

    public static IReadOnlyList<AgentService> All { get; } = [Aitunnel, ProxyApi, Provod];

    /// <summary>Services in the picker (the "Service" group in model settings); with a single service the group is hidden.</summary>
    public static IReadOnlyList<AgentService> Selectable { get; } = [Aitunnel];

    /// <returns>The service with the same host; <c>null</c> for a user's own endpoint.</returns>
    public static AgentService? For(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var address)
            ? All.FirstOrDefault(service => string.Equals(new Uri(service.Endpoint).Host, address.Host, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>API key name for the endpoint: each service has its own, others use the ProxyAPI key.</summary>
    public static string SecretFor(string endpoint) => For(endpoint)?.SecretName ?? AgentSecrets.ApiKey;

    /// <summary>Service dialect by endpoint; a user's own endpoint gets <see cref="ServiceDialect.Standard"/>.</summary>
    public static ServiceDialect DialectFor(string endpoint) => For(endpoint)?.Dialect ?? ServiceDialect.Standard;
}
