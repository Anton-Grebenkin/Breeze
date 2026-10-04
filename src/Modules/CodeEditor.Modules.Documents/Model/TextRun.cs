namespace CodeEditor.Modules.Documents.Model;

/// <summary>A text fragment with one style; a line break inside is a line break within the paragraph.</summary>
/// <param name="Link">Link address; <c>null</c> if the run is not a link.</param>
public readonly record struct TextRun(string Text, TextStyle Style = TextStyle.None, string? Link = null)
{
    public bool Has(TextStyle style) => (Style & style) == style;
}
