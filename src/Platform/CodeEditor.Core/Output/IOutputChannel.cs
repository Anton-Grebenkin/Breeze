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
}
