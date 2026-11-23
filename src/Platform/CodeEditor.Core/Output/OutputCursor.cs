namespace CodeEditor.Core.Output;

/// <summary>
/// How far a reader has read an output channel (<see cref="IOutputChannel.ReadSince"/>).
/// </summary>
/// <param name="Epoch">Changes when the channel removes text (clear, trim): what the reader shows no longer matches.</param>
/// <param name="Position">Characters read in this epoch.</param>
public readonly record struct OutputCursor(long Epoch, long Position)
{
    /// <summary>Nothing read yet: the first read returns the whole text.</summary>
    public static OutputCursor None { get; } = new(-1, 0);
}
