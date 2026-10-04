using System.Collections.Immutable;
using System.Text;
using CodeEditor.Modules.Documents.Model;

namespace CodeEditor.Modules.Documents.Formats;

/// <summary>
/// Builds text runs, merging adjacent pieces with the same style and link: Word splits text into runs for edits and
/// spell checking, while the model and the viewer need whole runs.
/// </summary>
internal sealed class RunsBuilder
{
    private readonly List<TextRun> _runs = [];
    private readonly StringBuilder _text = new();
    private TextStyle _style;
    private string? _link;

    public void Append(string text, TextStyle style = TextStyle.None, string? link = null)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_text.Length > 0 && (style != _style || link != _link))
        {
            Flush();
        }

        _style = style;
        _link = link;
        _text.Append(text);
    }

    /// <summary>Has visible text, not only spaces and line breaks.</summary>
    public bool HasText => _runs.Any(run => !string.IsNullOrWhiteSpace(run.Text)) || !string.IsNullOrWhiteSpace(_text.ToString());

    public ImmutableArray<TextRun> Build()
    {
        Flush();
        var runs = _runs.ToImmutableArray();
        _runs.Clear();
        return runs;
    }

    private void Flush()
    {
        if (_text.Length > 0)
        {
            _runs.Add(new TextRun(_text.ToString(), _style, _link));
            _text.Clear();
        }
    }
}
