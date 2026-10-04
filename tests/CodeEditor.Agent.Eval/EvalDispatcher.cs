using System.Collections.Concurrent;
using CodeEditor.Core.Threading;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Headless UI thread: a dedicated thread with a queue and its own synchronization context. Documents, tabs and the
/// chat feed change only on it, and continuations after <c>await</c> return to it, as in the app.
/// </summary>
internal sealed class EvalDispatcher : IUiDispatcher, IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public EvalDispatcher()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "eval-ui" };
        _thread.Start();
    }

    public Task InvokeAsync(Action action)
    {
        if (Thread.CurrentThread == _thread)
        {
            action();
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                action();
                done.SetResult();
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        });
        return done.Task;
    }

    /// <summary>Starts async work on the UI thread and waits for it to finish.</summary>
    public async Task RunAsync(Func<Task> work)
    {
        Task? running = null;
        await InvokeAsync(() => running = work());
        await running!;
    }

    public void Post(Action action) => _queue.Add(action);

    public void Dispose() => _queue.CompleteAdding();

    private void Run()
    {
        SynchronizationContext.SetSynchronizationContext(new QueueContext(this));
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            action();
        }
    }

    private sealed class QueueContext(EvalDispatcher dispatcher) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => dispatcher.Post(() => d(state));

        public override SynchronizationContext CreateCopy() => this;
    }
}
