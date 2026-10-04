using CodeEditor.Core.Files;

namespace CodeEditor.Testing;

/// <summary>
/// Test-driven watcher: <see cref="Raise"/> simulates a batch of changes, honoring exclusions.
/// </summary>
public sealed class FakeFileWatcher(Func<string, bool> isExcluded) : IFileWatcher
{
    public bool IsDisposed { get; private set; }

    public event EventHandler<FileChangesEventArgs>? Changed;

    public void Raise(params FileChange[] changes) =>
        Changed?.Invoke(this, new FileChangesEventArgs([.. changes.Where(change => !isExcluded(change.Path))], requiresRescan: false));

    public void RaiseRescan() => Changed?.Invoke(this, new FileChangesEventArgs([], requiresRescan: true));

    public void Dispose() => IsDisposed = true;
}
