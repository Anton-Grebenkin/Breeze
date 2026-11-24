namespace CodeEditor.Core.Output;

/// <summary>What an output channel gained since a reader's cursor.</summary>
/// <param name="Text">The appended text, or the whole text when <paramref name="IsReset"/>.</param>
/// <param name="IsReset">The channel removed text since the cursor: replace what is shown instead of appending.</param>
/// <param name="Cursor">The cursor for the next read.</param>
public readonly record struct OutputDelta(string Text, bool IsReset, OutputCursor Cursor)
{
    /// <summary>Nothing to show: no new text and no reset.</summary>
    public bool IsEmpty => !IsReset && Text.Length == 0;
}
