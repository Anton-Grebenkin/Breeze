namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>
/// A container log change for the view: append <see cref="Added"/>, then drop the first <see cref="Removed"/> lines
/// (the log keeps at most <see cref="ContainerLogViewModel.MaxLines"/>). <see cref="Reset"/> means the log was cleared.
/// </summary>
public sealed class LogLinesEventArgs(IReadOnlyList<string> added, int removed, bool reset) : EventArgs
{
    public IReadOnlyList<string> Added { get; } = added;

    public int Removed { get; } = removed;

    public bool Reset { get; } = reset;
}
