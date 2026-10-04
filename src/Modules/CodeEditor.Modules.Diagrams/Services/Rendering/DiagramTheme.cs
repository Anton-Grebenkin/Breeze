namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// The diagram rendering theme. The preview follows the editor theme; export and agent images are light, on a white
/// background, so the diagram reads well in documents, READMEs and for the model. A diagram's own
/// <c>%%{init: {"theme": "forest"}}%%</c> directive takes precedence.
/// </summary>
public enum DiagramTheme
{
    /// <summary>The Mermaid <c>default</c> theme.</summary>
    Light,

    /// <summary>The Mermaid <c>dark</c> theme.</summary>
    Dark,
}
