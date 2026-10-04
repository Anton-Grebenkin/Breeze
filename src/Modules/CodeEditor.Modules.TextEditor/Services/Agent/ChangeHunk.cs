namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// One agent change in a file, like a diff hunk without context: removed lines of the original and added lines of the
/// current text. Lines are 1-based. For a pure removal <see cref="AddedCount"/> is 0 and <see cref="NewStart"/> is the
/// current line the removed lines stood before (may be one past the last line).
/// </summary>
/// <param name="Index">0-based position in the file.</param>
/// <param name="OldStart">First removed line in the original (or the insertion point there).</param>
/// <param name="NewStart">First added line in the current text (or where the removed lines were).</param>
public sealed record ChangeHunk(int Index, int OldStart, IReadOnlyList<string> RemovedLines, int NewStart, int AddedCount)
{
    /// <summary>Last added line; for a pure removal, the line before the removal point.</summary>
    public int LastLine => NewStart + AddedCount - 1;

    public bool IsRemoval => AddedCount == 0;

    /// <summary>The current line belongs to the hunk: an added line or one next to the removal point.</summary>
    public bool Contains(int line) => line >= NewStart - (IsRemoval ? 1 : 0) && line <= Math.Max(NewStart, LastLine);
}
