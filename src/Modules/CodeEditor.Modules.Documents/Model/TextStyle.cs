namespace CodeEditor.Modules.Documents.Model;

/// <summary>Text style of a document fragment.</summary>
[Flags]
public enum TextStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strike = 8,

    /// <summary>Monospace font: inline code.</summary>
    Code = 16,
}
