namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>
/// The result of "Check the model": what each probe found, and what follows for the settings. A suggestion is
/// <c>null</c> when the probes give no ground to change what is guessed from the model's id.
/// </summary>
public sealed record ModelProbeReport(IReadOnlyList<ModelProbeStep> Steps)
{
    /// <summary>The model sees images.</summary>
    public bool? Images { get; init; }

    /// <summary>Where the reasoning shown in the feed should come from.</summary>
    public ReasoningSource? Reasoning { get; init; }

    /// <summary>The service wants the response limit as <c>max_tokens</c>; a setting of the whole connection.</summary>
    public bool LegacyMaxTokens { get; init; }

    /// <summary>The probes found something the settings can take.</summary>
    public bool HasSuggestions => Images is not null || Reasoning is not null || LegacyMaxTokens;
}
