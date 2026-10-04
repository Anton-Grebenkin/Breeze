using System.Collections.Frozen;

namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Model catalog: the quick-pick list when <c>agent.models</c> is unset, and context windows of known families for the
/// fill indicator. A specific model's exact window is set in <c>agent.contextWindow</c>.
/// </summary>
public static class ModelCatalog
{
    public const int FallbackContextWindow = 128_000;

    /// <summary>
    /// Supported models (ADR 0017), chosen by price/quality (Artificial Analysis intelligence index and Terminal-Bench,
    /// service prices): fast ones for simple tasks, optimal for everyday work and a powerful one for hard tasks. Prompts
    /// are tuned for them and the benchmark checks them. Ids cover all services: Provod and AITUNNEL write Claude
    /// versions with a dot, and where a model is missing the previous version is listed.
    /// </summary>
    public static IReadOnlyList<SupportedModel> Supported { get; } =
    [
        new("openai/gpt-6-luna", ModelTier.Fast, 1_050_000, 128_000),
        new("deepseek/deepseek-v4.1-flash", ModelTier.Fast, 1_048_576, 393_216),
        new("openai/gpt-6.1-sol", ModelTier.Optimal, 1_050_000, 128_000),
        new("openai/gpt-6-sol", ModelTier.Optimal, 1_050_000, 128_000),
        new("anthropic/claude-sonnet-5-5", ModelTier.Optimal, 1_000_000, 128_000),
        new("anthropic/claude-sonnet-5.5", ModelTier.Optimal, 1_000_000, 128_000),
        new("anthropic/claude-sonnet-5", ModelTier.Optimal, 1_000_000, 128_000),
        new("x-ai/grok-4.7", ModelTier.Optimal, 500_000, 450_000),
        new("anthropic/claude-opus-5-5", ModelTier.Powerful, 1_000_000, 128_000),
        new("anthropic/claude-opus-5.5", ModelTier.Powerful, 1_000_000, 128_000),
    ];

    /// <summary>Provod models in the picker; the first one is the default.</summary>
    public static IReadOnlyList<string> ProvodModels { get; } =
    [
        "openai/gpt-6-luna", "deepseek/deepseek-v4.1-flash", "openai/gpt-6-sol", "anthropic/claude-sonnet-5", "x-ai/grok-4.7", "anthropic/claude-opus-5.5",
    ];

    /// <summary>The same models at ProxyAPI, which also has the newer GPT-6.1 Sol and Claude Sonnet 5.5.</summary>
    public static IReadOnlyList<string> ProxyApiModels { get; } =
    [
        "openai/gpt-6-luna", "deepseek/deepseek-v4.1-flash", "openai/gpt-6.1-sol", "anthropic/claude-sonnet-5-5", "x-ai/grok-4.7", "anthropic/claude-opus-5-5",
    ];

    /// <summary>
    /// The same models at AITUNNEL, the default and only selectable service. Claude versions use a dot; requests send the
    /// id without the vendor (<see cref="ServiceDialect.ShortModelIds"/>).
    /// </summary>
    public static IReadOnlyList<string> AitunnelModels { get; } =
    [
        "openai/gpt-6-luna", "deepseek/deepseek-v4.1-flash", "openai/gpt-6.1-sol", "anthropic/claude-sonnet-5.5", "x-ai/grok-4.7", "anthropic/claude-opus-5.5",
    ];

    /// <summary>Models for an endpoint outside <see cref="AgentServices"/> and for the manager catalog: the default service's.</summary>
    public static IReadOnlyList<string> DefaultModels => AitunnelModels;

    private static readonly FrozenDictionary<string, SupportedModel> SupportedById =
        Supported.ToFrozenDictionary(model => model.Id, StringComparer.OrdinalIgnoreCase);

    /// <returns><c>null</c> if the model is not supported.</returns>
    public static SupportedModel? SupportedFor(string model) => SupportedById.GetValueOrDefault(model);

    // First substring match wins: narrow families before broad ones.
    private static readonly (string Marker, int Tokens)[] ContextWindows =
    [
        ("claude-haiku", 200_000),
        ("claude-opus-4-1", 200_000),
        ("-4-5", 200_000),
        ("claude", 1_000_000),
        ("gpt-6", 1_050_000),
        ("gpt-5.", 1_000_000),
        ("gpt-4.1", 1_047_576),
        ("gpt-5", 400_000),
        ("gpt-4o", 128_000),
        ("o3", 200_000),
        ("o4", 200_000),
        ("gemini", 1_048_576),
        ("deepseek-v4", 1_050_000),
        ("deepseek", 128_000),
        ("qwen", 128_000),
        ("grok", 256_000),
        ("kimi", 256_000),
        ("glm", 200_000),
        ("minimax", 1_000_000),
        ("devstral", 256_000),
    ];

    /// <summary>Models in the input and role pickers: the current service's models, always including the current one.</summary>
    public static IReadOnlyList<string> Choices(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var models = AgentServices.For(options.Endpoint)?.Models ?? DefaultModels;
        return models.Contains(options.Model, StringComparer.OrdinalIgnoreCase) ? models : [options.Model, .. models];
    }

    /// <summary>Models enabled in the manager (hidden while the choice is limited): from settings, else the catalog.</summary>
    public static IReadOnlyList<string> ModelsFor(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var models = options.Models.Count > 0 ? options.Models : DefaultModels;
        return models.Contains(options.Model, StringComparer.OrdinalIgnoreCase) ? models : [options.Model, .. models];
    }

    public static int ContextWindowFor(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ContextWindow is > 0 and var configured)
        {
            return configured;
        }

        if (SupportedFor(options.Model) is { } supported)
        {
            return supported.ContextWindow;
        }

        foreach (var (marker, tokens) in ContextWindows)
        {
            if (options.Model.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return tokens;
            }
        }

        return FallbackContextWindow;
    }
}
