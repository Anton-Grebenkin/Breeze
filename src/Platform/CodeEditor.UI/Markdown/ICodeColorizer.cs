using System.Windows.Documents;

namespace CodeEditor.UI.Markdown;

/// <summary>
/// Syntax colors for Markdown code blocks using theme colors. Implemented by the editor module, which owns the language
/// definitions; without it blocks are shown in one color.
/// </summary>
public interface ICodeColorizer
{
    /// <summary>Colors changed (theme switch): shown blocks need recoloring.</summary>
    event EventHandler? Changed;

    /// <summary>Colored code lines with line breaks; <c>null</c> if the language is unknown.</summary>
    /// <param name="language">Language from the block fence: <c>csharp</c>, <c>json</c>, <c>xml</c>…</param>
    IReadOnlyList<Inline>? Colorize(string code, string language);
}
