using CodeEditor.Core.Documents;

namespace CodeEditor.Testing;

/// <summary>
/// Test buffer: text as a string, undo history as snapshots. Not meant for large texts.
/// </summary>
public sealed class TestTextBuffer(string text) : ITextBuffer
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private string _text = text;
    private int _savedVersion;
    private int _version;
    private int _nextVersion = 1;
    private readonly Stack<int> _undoVersions = new();
    private readonly Stack<int> _redoVersions = new();

    public int Length => _text.Length;

    public bool IsModified => _version != _savedVersion;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? Changed;

    public event EventHandler? ModifiedChanged;

    public string GetText() => _text;

    public string GetText(int offset, int length) => _text.Substring(offset, length);

    public void Replace(int offset, int length, string text) =>
        Apply(() =>
        {
            _undo.Push(_text);
            _undoVersions.Push(_version);
            _redo.Clear();
            _redoVersions.Clear();
            _text = string.Concat(_text.AsSpan(0, offset), text, _text.AsSpan(offset + length));
            _version = _nextVersion++;
        });

    public void ReplaceAll(IReadOnlyList<TextReplacement> replacements) =>
        Apply(() =>
        {
            _undo.Push(_text);
            _undoVersions.Push(_version);
            _redo.Clear();
            _redoVersions.Clear();
            foreach (var replacement in replacements.OrderByDescending(replacement => replacement.Offset))
            {
                _text = string.Concat(_text.AsSpan(0, replacement.Offset), replacement.Text, _text.AsSpan(replacement.Offset + replacement.Length));
            }

            _version = _nextVersion++;
        });

    public void Undo() =>
        Apply(() =>
        {
            _redo.Push(_text);
            _redoVersions.Push(_version);
            _text = _undo.Pop();
            _version = _undoVersions.Pop();
        });

    public void Redo() =>
        Apply(() =>
        {
            _undo.Push(_text);
            _undoVersions.Push(_version);
            _text = _redo.Pop();
            _version = _redoVersions.Pop();
        });

    public void MarkSaved() => Apply(() => _savedVersion = _version);

    public void Reset(string text) =>
        Apply(() =>
        {
            _undo.Clear();
            _redo.Clear();
            _undoVersions.Clear();
            _redoVersions.Clear();
            _text = text;
            _version = _nextVersion++;
        });

    private void Apply(Action change)
    {
        var wasModified = IsModified;
        var before = _text;
        change();

        if (!ReferenceEquals(before, _text))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        if (wasModified != IsModified)
        {
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
