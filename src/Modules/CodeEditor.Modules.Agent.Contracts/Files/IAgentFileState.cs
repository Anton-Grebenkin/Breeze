namespace CodeEditor.Modules.Agent.Contracts.Files;

/// <summary>
/// What the agent knows about files in the current chat, like Claude Code's "read before edit": which text version it
/// saw. An edit is allowed only in a file that was read and has not changed since, otherwise the model edits stale text.
/// Re-reading an unchanged window costs no tokens. Implemented by the agent module; a new chat starts clean.
/// </summary>
public interface IAgentFileState
{
    /// <summary>The agent read a line window of a file (full path) with the whole file text.</summary>
    /// <returns>
    /// <c>true</c> on the second read of this window of the same version, so "unchanged" may be returned; from the
    /// third read on, the text is returned again.
    /// </returns>
    bool RecordRead(string path, string text, int firstLine, int lastLine);

    /// <summary>Check before an edit.</summary>
    /// <returns><c>null</c> if the edit may proceed; otherwise an explanation for the model of what to do.</returns>
    string? CheckEditable(string path, string relativePath, string currentText);

    /// <summary>The agent changed, created or deleted a file: it knows this version; the original is kept for the diff.</summary>
    /// <param name="text">New text; <c>null</c> if the file was deleted.</param>
    /// <param name="previousText">Text before the edit; <c>null</c> if the file did not exist.</param>
    void RecordWrite(string path, string? text, string? previousText);

    /// <summary>Files the agent changed in this chat: path → text before the first edit (<c>null</c> if it created them).</summary>
    IReadOnlyDictionary<string, string?> Changes { get; }

    /// <summary>The agent changed, created or deleted a file (full path); raised on the tool thread.</summary>
    event EventHandler<string>? Written;

    /// <summary>The diff baseline changed (a hunk or file accepted, a file untracked); <c>null</c> means all files.</summary>
    event EventHandler<string?>? ChangesChanged;

    /// <summary>The user accepted part of a file's edits: the diff is now computed from the new original.</summary>
    void UpdateOriginal(string path, string original);

    /// <summary>The user accepted all edits of a file: it is no longer in <see cref="Changes"/>.</summary>
    void AcceptFile(string path);

    /// <summary>
    /// The user rejected agent edits in a file (a hunk or all): the model learns about it at the start of the next
    /// message and re-reads the file before editing.
    /// </summary>
    void RecordRejected(string path);
}
