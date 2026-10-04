namespace CodeEditor.Core.Documents;

/// <summary>
/// Document text with undo history. The app uses AvalonEdit's <c>TextDocument</c> (ADR 0003), tests use a simple
/// buffer. Bound to the UI thread.
/// </summary>
public interface ITextBuffer
{
    int Length { get; }

    /// <summary>Changed since the last save, according to the undo history.</summary>
    bool IsModified { get; }

    bool CanUndo { get; }

    bool CanRedo { get; }

    event EventHandler? Changed;

    event EventHandler? ModifiedChanged;

    string GetText();

    string GetText(int offset, int length);

    /// <summary>Replaces a range; the edit goes to the undo history.</summary>
    void Replace(int offset, int length, string text);

    /// <summary>
    /// Applies several replacements as one undo step, so a file-wide agent edit is undone with one <c>Ctrl+Z</c>.
    /// Offsets refer to the original text; ranges do not overlap.
    /// </summary>
    void ReplaceAll(IReadOnlyList<TextReplacement> replacements);

    void Undo();

    void Redo();

    /// <summary>Marks the current state as saved: <see cref="IsModified"/> becomes <c>false</c>.</summary>
    void MarkSaved();

    /// <summary>Replaces the whole text without undo history; used to reload the file from disk.</summary>
    void Reset(string text);
}
