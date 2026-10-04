using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Per-model request options (ADR 0010): temperature only for models that accept it; reasoning effort from settings,
/// plus a summary for GPT, which OpenAI does not send unless asked (the feed's reasoning block stays empty). For models
/// with native <c>reasoning_content</c> the default effort is sent as medium: without it Gemini, Grok and Qwen Thinking
/// reason but do not return the reasoning text.
/// </summary>
public static class ModelRequest
{
    public static float? Temperature(AgentOptions options, ModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(profile);
        return profile.AcceptsTemperature ? (float?)options.Temperature : null;
    }

    /// <returns><c>null</c> to use the model defaults.</returns>
    public static ReasoningOptions? Reasoning(AgentReasoningEffort effort, ModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var level = effort == AgentReasoningEffort.Default && profile.Reasoning == ReasoningSource.Native ? ReasoningEffort.Medium : Level(effort);
        var summary = profile.RequestsReasoningSummary && level != ReasoningEffort.None;
        return level is null && !summary ? null : new ReasoningOptions { Effort = level, Output = summary ? ReasoningOutput.Full : null };
    }

    /// <summary>The model has no native reasoning and reasoning is not off, so the prompt asks it to think aloud.</summary>
    public static bool ThinksAloud(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ReasoningEffort != AgentReasoningEffort.None && ModelProfiles.For(options.Model).Reasoning == ReasoningSource.ThinkAloud;
    }

    private static ReasoningEffort? Level(AgentReasoningEffort effort) => effort switch
    {
        AgentReasoningEffort.None => ReasoningEffort.None,
        AgentReasoningEffort.Low => ReasoningEffort.Low,
        AgentReasoningEffort.Medium => ReasoningEffort.Medium,
        AgentReasoningEffort.High => ReasoningEffort.High,
        AgentReasoningEffort.ExtraHigh => ReasoningEffort.ExtraHigh,
        _ => null,
    };
}
