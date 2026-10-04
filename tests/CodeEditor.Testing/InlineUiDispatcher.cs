using CodeEditor.Core.Threading;

namespace CodeEditor.Testing;

/// <summary>
/// The tests' "UI thread": actions run immediately on the calling thread.
/// </summary>
public sealed class InlineUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public void Post(Action action) => action();
}
