namespace CodeEditor.Modules.Diagrams.ViewModels;

/// <summary>A diagram in the preview: the rendered SVG and a caption.</summary>
/// <param name="Index">The diagram number in the file, from 1.</param>
/// <param name="Svg">SVG markup from the last successful render.</param>
/// <param name="IsStale">The text has an error: the last successful image is shown, not the current text.</param>
public sealed record DiagramPreviewItem(int Index, string Svg, bool IsStale)
{
    /// <summary>The caption above Markdown blocks ("Diagram 2 · line 14"); <c>null</c> for a Mermaid file.</summary>
    public string? Caption { get; init; }
}
