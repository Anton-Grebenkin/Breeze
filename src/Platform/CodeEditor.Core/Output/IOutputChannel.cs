namespace CodeEditor.Core.Output;

/// <summary>
/// Output channel: the log, build output, the agent. Writable from any thread.
/// </summary>
public interface IOutputChannel
{
    string Name { get; }

    /// <summary>Grows on every change; the view redraws only when the version changes.</summary>
    long Version { get; }

    void AppendLine(string text);

    void Clear();

    /// <summary>The channel's whole current text.</summary>
    string Snapshot();

    /// <summary>
    /// The text appended since <paramref name="cursor"/>, so a view appends instead of copying the whole channel on
    /// every change; the whole text if the channel removed some since. The default suits a small channel: any change
    /// returns everything.
    /// </summary>
    OutputDelta ReadSince(OutputCursor cursor) =>
        cursor.Epoch == Version ? new OutputDelta(string.Empty, IsReset: false, cursor) : new OutputDelta(Snapshot(), IsReset: true, new OutputCursor(Version, 0));
}
