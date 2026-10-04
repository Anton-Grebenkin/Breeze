namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>A diagram in a file: a whole <c>.mmd</c> file or a <c>```mermaid</c> block in Markdown.</summary>
/// <param name="Text">The Mermaid text.</param>
/// <param name="FirstLine">The file line (from 1) where the diagram text starts; maps diagram errors to file lines.</param>
/// <param name="Index">
/// The diagram number in the file, from 1: the preview caption, the export file name, the agent <c>block</c> argument.
/// </param>
public sealed record DiagramSource(string Text, int FirstLine, int Index)
{
    /// <summary>The file line for a diagram text line (from 1); <c>null</c> means the first diagram line.</summary>
    public int FileLine(int? diagramLine) => FirstLine + Math.Max(diagramLine ?? 1, 1) - 1;
}
