namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>
/// What the user says about a model of their connection (ADR 0055): a gateway names models its own way, so the name
/// tells nothing about the model behind it. Whatever is not set is guessed from the id, as for any model
/// (<see cref="ModelProfiles"/>).
/// </summary>
public sealed class ModelDescription
{
    /// <summary>The model id the service expects.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name shown in the editor; the id when empty.</summary>
    public string? Name { get; set; }

    /// <summary>Which model it is: decides the prompt adjustments and how reasoning and edits work by default.</summary>
    public ModelFamily? Family { get; set; }

    /// <summary>Context window in tokens.</summary>
    public int? ContextWindow { get; set; }

    /// <summary>The longest response the model can return, in tokens.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>The model sees images.</summary>
    public bool? Images { get; set; }

    /// <summary>Where the reasoning shown in the feed comes from.</summary>
    public ReasoningSource? Reasoning { get; set; }

    /// <summary>The edit tool the model gets.</summary>
    public EditFormat? EditFormat { get; set; }
}
