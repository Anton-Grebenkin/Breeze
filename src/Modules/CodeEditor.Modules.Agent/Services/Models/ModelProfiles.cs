using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Profile by model id (<c>vendor/model</c>): family, texts from <see cref="FamilyPrompts"/> and request settings.
/// Adjustments follow Copilot, Cursor and Claude Code experience (ADR 0010): Claude tends to over-explore, older GPT
/// stops mid-work, Gemini describes calls in text, weak models need short imperative rules and one step at a time.
/// </summary>
public static partial class ModelProfiles
{
    // First substring match wins: narrow families before broad ones.
    private static readonly (string Marker, ModelFamily Family)[] Families =
    [
        ("claude", ModelFamily.Claude),
        ("gpt-6", ModelFamily.GptReasoning),
        ("gpt-5", ModelFamily.GptReasoning),
        ("gpt-4", ModelFamily.Gpt),
        ("gpt-3", ModelFamily.Gpt),
        ("gemini", ModelFamily.Gemini),
        ("deepseek", ModelFamily.DeepSeek),
        ("grok", ModelFamily.Frontier),
        ("qwen3", ModelFamily.Frontier),
        ("qwen-3", ModelFamily.Frontier),
        ("kimi", ModelFamily.Frontier),
        ("glm", ModelFamily.Frontier),
        ("minimax", ModelFamily.Frontier),
        ("mimo", ModelFamily.Frontier),
    ];

    private static readonly string[] ReasoningMarkers = ["gpt-6", "gpt-5", "reasoner", "-r1", "thinking", "qwq"];

    /// <summary>
    /// Models without native reasoning: Qwen Coder, Mistral and Devstral, Llama, Gemma, DeepSeek Chat (V3). Not Grok: it
    /// does reason and sends a summary in <c>reasoning</c> (<see cref="ReasoningFieldPolicy"/>), and ignores requests to
    /// reason in tags when tools are present.
    /// </summary>
    private static readonly string[] WithoutOwnReasoning =
        ["-coder", "coder-", "devstral", "codestral", "mistral", "ministral", "mixtral", "llama", "gemma", "deepseek-chat", "deepseek-v3"];

    private static readonly string[] OpenAiReasoningPrefixes = ["o1", "o3", "o4"];

    /// <summary>
    /// Default response limit for Claude and supported models (capped at their maximum). Only written tokens are paid
    /// for, and a cut-off reply is continued (ChecksExecutor). Without a limit ProxyAPI checks the balance against
    /// "input + longest possible reply", so with a small balance cheap requests got 402.
    /// </summary>
    private const int OutputTokenCap = 32_000;

    /// <summary>From this version on Claude reasons in "adaptive" mode and rejects a token budget.</summary>
    private const int ClaudeAdaptiveThinkingSince = 5;

    public static ModelProfile For(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var name = model[(model.LastIndexOf('/') + 1)..].ToLowerInvariant();
        var family = FamilyOf(name);
        var (guidance, reminder) = FamilyPrompts.For(family);
        return new ModelProfile(family, IsReasoning(name), guidance, reminder)
        {
            DefaultMaxOutputTokens = ModelCatalog.SupportedFor(model) is { } supported
                ? Math.Min(OutputTokenCap, supported.MaxOutputTokens)
                : family == ModelFamily.Claude ? OutputTokenCap : null,
            AcceptsTemperature = family is not (ModelFamily.Claude or ModelFamily.GptReasoning),
            RequestsReasoningSummary = family == ModelFamily.GptReasoning,
            EditFormat = family == ModelFamily.GptReasoning ? EditFormat.Patch : EditFormat.Replace,
            Thinking = family == ModelFamily.Claude ? ThinkingOf(name) : ClaudeThinking.None,
            Reasoning = ReasoningOf(family, name),
            SeesImages = SeesImages(family, name),
        };
    }

    // Native reasoning comes from GPT (summary), Claude (thinking) and most open models (reasoning_content); coding
    // models and older chat models lack it, so the prompt tells them to think aloud.
    private static ReasoningSource ReasoningOf(ModelFamily family, string name) => family switch
    {
        ModelFamily.GptReasoning => ReasoningSource.Summary,
        ModelFamily.Claude => ReasoningSource.Thinking,
        ModelFamily.Gpt => ReasoningSource.ThinkAloud,
        _ => WithoutOwnReasoning.Any(marker => name.Contains(marker, StringComparison.Ordinal)) ? ReasoningSource.ThinkAloud : ReasoningSource.Native,
    };

    // Open Frontier models are mostly text-only, except Grok and models with "-vl" in the name.
    private static bool SeesImages(ModelFamily family, string name) =>
        family is ModelFamily.Claude or ModelFamily.GptReasoning or ModelFamily.Gpt or ModelFamily.Gemini
        || name.Contains("grok", StringComparison.Ordinal) || name.Contains("-vl", StringComparison.Ordinal);

    // "claude-haiku-4-5", "claude-3-7-sonnet" use a budget; "claude-sonnet-5" and unversioned names are adaptive.
    private static ClaudeThinking ThinkingOf(string name) =>
        ClaudeVersion().Match(name) is { Success: true } match
        && int.Parse(match.Groups["major"].ValueSpan, CultureInfo.InvariantCulture) < ClaudeAdaptiveThinkingSince
            ? ClaudeThinking.Budget
            : ClaudeThinking.Adaptive;

    [GeneratedRegex(@"claude-(?:[a-z]+-)?(?<major>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex ClaudeVersion();

    private static ModelFamily FamilyOf(string name)
    {
        if (OpenAiReasoningPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return ModelFamily.GptReasoning;
        }

        foreach (var (marker, family) in Families)
        {
            if (name.Contains(marker, StringComparison.Ordinal))
            {
                return family;
            }
        }

        return ModelFamily.Other;
    }

    private static bool IsReasoning(string name) =>
        OpenAiReasoningPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
        || ReasoningMarkers.Any(marker => name.Contains(marker, StringComparison.Ordinal));
}
