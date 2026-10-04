using System.Threading.Channels;
using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>
/// A queued "UI thread": posted actions run on the test thread when it takes them, as in a window where a background
/// read's result arrives after the calling method has returned.
/// </summary>
internal sealed class QueueDispatcher : IUiDispatcher
{
    private readonly Channel<Action> _posted = Channel.CreateUnbounded<Action>();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public void Post(Action action) => _posted.Writer.TryWrite(action);

    /// <summary>Waits for the next posted action and runs it.</summary>
    public async Task RunNextAsync()
    {
        var action = await _posted.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        action();
    }
}
