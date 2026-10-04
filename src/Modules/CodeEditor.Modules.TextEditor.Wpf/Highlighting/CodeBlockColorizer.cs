using System.Windows.Documents;
using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.UI.Markdown;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Modules.TextEditor.Wpf.Highlighting;

/// <summary>
/// Highlights Markdown code blocks (agent replies) with the editor's definitions and theme colors.
/// The fence language (<c>csharp</c>, <c>ts</c>, <c>bash</c>, <c>Dockerfile</c>) is looked up in the language catalog.
/// </summary>
/// <remarks>O(n) in code length: the AvalonEdit highlighter visits each line once.</remarks>
public sealed class CodeBlockColorizer : ICodeColorizer
{
    private readonly ThemedHighlighting _highlighting;

    public CodeBlockColorizer(ThemedHighlighting highlighting)
    {
        _highlighting = highlighting;
        _highlighting.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<Inline>? Colorize(string code, string language)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(language);

        if (_highlighting.For(LanguageCatalog.ForAlias(language)) is not { } definition)
        {
            return null;
        }

        var document = new TextDocument(code);
        var highlighter = new DocumentHighlighter(document, definition);
        var inlines = new List<Inline>();
        for (var line = 1; line <= document.LineCount; line++)
        {
            if (line > 1)
            {
                inlines.Add(new LineBreak());
            }

            inlines.AddRange(highlighter.HighlightLine(line).ToRichText().CreateRuns());
        }

        return inlines;
    }
}
