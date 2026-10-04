using System.Collections.Frozen;
using CodeEditor.Modules.Agent.Services.Api;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Model price at a service in rubles per 1M tokens: input, output, cache reads, and the cache write premium (a
/// multiplier on the input price for everything not read from the cache). Prices differ between services, so they are
/// looked up by the run's service. Models missing from the tables get no cost in the report.
/// </summary>
internal sealed record ModelPrice(decimal Input, decimal Output, decimal CachedInput, decimal CacheWrite = 1m)
{
    private const decimal Million = 1_000_000m;

    // AITUNNEL cache writes (docs, 02.10.2026): Claude ×1.25 for 5 minutes (with cache marks Claude writes everything
    // not read), GPT-5.6 and newer ×1.25. The one-hour warmup (×2) writes only the model answer and is ignored here.
    private const decimal CacheWritePremium = 1.25m;

    // ProxyAPI, VAT included, September 2026.
    private static readonly FrozenDictionary<string, ModelPrice> ProxyApi = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
    {
        ["deepseek/deepseek-v4-flash"] = new(20m, 40m, 4m),
        ["openai/gpt-6-luna"] = new(30m, 150m, 3m),
        ["openai/gpt-5-mini"] = new(65m, 516m, 6.45m),
        ["anthropic/claude-sonnet-5"] = new(600m, 3_030m, 60m),
        ["anthropic/claude-sonnet-4-6"] = new(774m, 3_866m, 77.4m),
        ["anthropic/claude-haiku-4-5"] = new(295m, 1_474m, 29.5m),

        // ProxyAPI catalog, 29.09.2026. Unknown cache read prices count as regular input: an upper bound, so the spend
        // caps (--budget, --max-task-cost) can't be exceeded because of them.
        ["openai/gpt-5.4-nano"] = new(61m, 380m, 6.1m),
        ["openai/gpt-5.4-mini"] = new(230m, 1_370m, 23m),
        ["google/gemini-3.5-flash-lite"] = new(91m, 758m, 91m),
        ["google/gemini-3.8-flash"] = new(455m, 2_275m, 45m),
        ["deepseek/deepseek-v4.1-flash"] = new(41.05m, 168.42m, 0.81m),
        ["x-ai/grok-4.3"] = new(168.42m, 336.84m, 168.42m),
        ["x-ai/grok-4.7"] = new(221.05m, 652.63m, 53.68m),
        ["qwen/qwen3.8-flash"] = new(20m, 65m, 20m),
        ["qwen/qwen3-coder-plus"] = new(200m, 442.11m, 200m),
        ["moonshotai/kimi-k2.7-code"] = new(125m, 525m, 125m),
        ["z-ai/glm-5.3-flash"] = new(16.84m, 58.95m, 16.84m),
        ["mistralai/devstral-2512"] = new(53.68m, 273.68m, 53.68m),
        ["minimax/minimax-m3"] = new(41.05m, 168.42m, 41.05m),

        // Supported models (ADR 0017), ProxyAPI catalog, 30.09.2026. Cache writes (pricier than input at Anthropic and
        // OpenAI) are ignored, so the fresh part of a request is a lower bound.
        ["openai/gpt-6-sol"] = new(420m, 2_100m, 42m),
        ["openai/gpt-6-astra"] = new(1_580m, 7_900m, 158m),
        ["anthropic/claude-sonnet-5-5"] = new(500m, 2_500m, 50m),
        ["anthropic/claude-opus-5-5"] = new(840m, 4_200m, 42m),
        ["anthropic/claude-fable-5-1"] = new(1_580m, 7_900m, 40m),
        ["google/gemini-3.1-pro-preview"] = new(600m, 3_640m, 58m),
        ["deepseek/deepseek-v4-pro"] = new(190m, 375m, 16m),

        // Model set of 02.10.2026 (ADR 0017 amendment), ProxyAPI catalog.
        ["openai/gpt-6.1-sol"] = new(420m, 2_100m, 21m),
        ["xiaomi/mimo-v2.6-pro"] = new(58.95m, 126.32m, 0.48m),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Provod catalog, 02.10.2026 (llms-full.txt): input, output and cache reads.
    private static readonly FrozenDictionary<string, ModelPrice> Provod = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
    {
        ["openai/gpt-6-luna"] = new(8.32m, 41.62m, 0.83m),
        ["openai/gpt-6-sol"] = new(166.49m, 832.45m, 16.65m),
        ["openai/gpt-6-astra"] = new(832.45m, 4_162.27m, 83.25m),
        ["anthropic/claude-sonnet-5"] = new(166.49m, 832.45m, 16.65m),
        ["anthropic/claude-opus-5.5"] = new(332.98m, 1_664.91m, 16.65m),
        ["anthropic/claude-fable-5.1"] = new(832.45m, 4_162.27m, 20.81m),
        ["x-ai/grok-4.7"] = new(166.49m, 499.47m, 41.62m),
        ["deepseek/deepseek-v4.1-flash"] = new(24.97m, 99.89m, 0.50m),
        ["xiaomi/mimo-v2.6-pro"] = new(36.21m, 72.42m, 0.30m),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // AITUNNEL catalog, 02.10.2026 (public model list): input and output; cache reads are input × (1 − cache_discount).
    private static readonly FrozenDictionary<string, ModelPrice> Aitunnel = new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
    {
        ["openai/gpt-6-luna"] = new(20m, 100m, 2m, CacheWritePremium),
        ["deepseek/deepseek-v4.1-flash"] = new(30m, 120m, 0.6m),
        ["xiaomi/mimo-v2.6-pro"] = new(87m, 174m, 0.72m),
        ["openai/gpt-6.1-sol"] = new(400m, 2_000m, 20m, CacheWritePremium),
        ["anthropic/claude-sonnet-5.5"] = new(400m, 2_000m, 40m, CacheWritePremium),
        ["x-ai/grok-4.7"] = new(320m, 960m, 80m),
        ["anthropic/claude-opus-5.5"] = new(800m, 4_000m, 40m, CacheWritePremium),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <param name="service">Run service title (<see cref="EvalOptions.Service"/>); others use ProxyAPI prices.</param>
    public static ModelPrice? For(string model, string service) => TableOf(service).GetValueOrDefault(model);

    private static FrozenDictionary<string, ModelPrice> TableOf(string service) =>
        string.Equals(service, AgentServices.Provod.Title, StringComparison.OrdinalIgnoreCase) ? Provod
        : string.Equals(service, AgentServices.Aitunnel.Title, StringComparison.OrdinalIgnoreCase) ? Aitunnel
        : ProxyApi;

    /// <summary>
    /// Cost in rubles: cached input at the cache price, the rest at the input price with the write premium.
    /// </summary>
    public decimal Cost(long input, long cachedInput, long output) =>
        ((input - cachedInput) * Input * CacheWrite + cachedInput * CachedInput + output * Output) / Million;
}
