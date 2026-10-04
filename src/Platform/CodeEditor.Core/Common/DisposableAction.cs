namespace CodeEditor.Core.Common;

/// <summary>
/// Runs the action on the first <see cref="Dispose"/> call; later calls are ignored.
/// </summary>
internal sealed class DisposableAction(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
