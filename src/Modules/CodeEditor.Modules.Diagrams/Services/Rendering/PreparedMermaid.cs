namespace CodeEditor.Modules.Diagrams.Services.Rendering;

/// <summary>
/// Diagram text for Mermaid (<see cref="MermaidSource.Prepare"/>) and the line offset: Mermaid numbers error lines
/// after stripping front matter, directives and leading blank lines.
/// </summary>
/// <param name="Text">Text for Mermaid: <c>\n</c> line breaks, <c>%%</c> comment lines blanked.</param>
/// <param name="LeadingLines">How many leading lines Mermaid drops before the diagram.</param>
/// <param name="LineCount">The number of lines in the text.</param>
public readonly record struct PreparedMermaid(string Text, int LeadingLines, int LineCount)
{
    /// <summary>The diagram text line (from 1) for a Mermaid error line; without a line, the first diagram line.</summary>
    public int TextLine(int? mermaidLine) => Math.Clamp(LeadingLines + Math.Max(mermaidLine ?? 1, 1), 1, Math.Max(LineCount, 1));
}
