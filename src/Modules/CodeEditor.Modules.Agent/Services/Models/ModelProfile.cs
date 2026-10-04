namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// A model "passport" for the harness, like Copilot's prompt registry (ADR 0010): family, whether the model reasons on
/// its own (then no "think step by step" requests), prompt adjustments and a reminder against typical family failures,
/// and request settings.
/// </summary>
/// <param name="Guidance">System prompt section for the family; empty for none.</param>
/// <param name="Reminder">Reminder in the last user message; empty for none.</param>
public sealed record ModelProfile(ModelFamily Family, bool IsReasoning, string Guidance, string Reminder)
{
    /// <summary>
    /// Response length when <c>agent.maxOutputTokens</c> is unset; <c>null</c> for the service default. Claude's default
    /// via ProxyAPI is small and includes reasoning, so replies were cut off before any text.
    /// </summary>
    public int? DefaultMaxOutputTokens { get; init; }

    /// <summary>Whether to send temperature: Claude 5 and reasoning GPT answer 400 to anything but the default.</summary>
    public bool AcceptsTemperature { get; init; } = true;

    /// <summary>
    /// Ask for a reasoning summary (Responses API): GPT does not send one unless asked, leaving the feed's reasoning
    /// block empty.
    /// </summary>
    public bool RequestsReasoningSummary { get; init; }

    /// <summary>The family's edit tool; the other one is hidden to cut schemas and the temptation to mix formats.</summary>
    public EditFormat EditFormat { get; init; }

    /// <summary>How to enable Claude reasoning; <see cref="ClaudeThinking.None"/> for other families.</summary>
    public ClaudeThinking Thinking { get; init; }

    /// <summary>The model sees images: GPT, Claude, Gemini, Grok. Others do not get the image viewer.</summary>
    public bool SeesImages { get; init; }

    /// <summary>Where reasoning in the feed comes from: native, or aloud at the prompt's request.</summary>
    public ReasoningSource Reasoning { get; init; } = ReasoningSource.Native;

    // Editor module tool names as literals: the agent does not reference that module's implementation.
    public bool Allows(string toolName) => toolName switch
    {
        "apply_edits" => EditFormat == EditFormat.Replace,
        "apply_patch" => EditFormat == EditFormat.Patch,
        ImageAgentTools.ViewImageName => SeesImages,
        _ => true,
    };
}
