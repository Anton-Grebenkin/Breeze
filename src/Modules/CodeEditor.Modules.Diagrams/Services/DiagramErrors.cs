using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// Diagram errors for people and the model, with file lines instead of diagram lines. Mermaid writes "Parse error on
/// line 3:" counting from the diagram start, but in Markdown the diagram starts mid-file, so the Mermaid number is
/// removed and a "Line 14:" caption names the file line. The rest is Mermaid's message: a fragment with "^" under the
/// error position and the expected tokens.
/// </summary>
public static partial class DiagramErrors
{
    public static DiagramProblem Locate(DiagramSource source, DiagramError error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(error);
        var message = RelativeLine().Replace(error.Message.ReplaceLineEndings("\n").Trim(), string.Empty);
        return new DiagramProblem(source.Index, source.FileLine(error.Line), message.Length == 0 ? Strings.UnknownDiagramError : message);
    }

    /// <summary>The full text: "Line 14: Parse error:" and the fragment with the error position.</summary>
    public static string Describe(DiagramProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return string.Format(CultureInfo.CurrentCulture, Strings.ErrorAtLine, problem.Line, problem.Message);
    }

    /// <summary>
    /// One line for the status bar: the header and the last message line, where Mermaid says what it expected.
    /// </summary>
    public static string Brief(DiagramProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var lines = problem.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var text = lines.Length > 1 ? $"{lines[0].TrimEnd(':')}: {lines[^1]}" : problem.Message;
        return string.Format(CultureInfo.CurrentCulture, Strings.ErrorAtLine, problem.Line, text);
    }

    // "on line 3" counts from the diagram start, which is a different line in the file.
    [GeneratedRegex(@" on line \d+")]
    private static partial Regex RelativeLine();
}
