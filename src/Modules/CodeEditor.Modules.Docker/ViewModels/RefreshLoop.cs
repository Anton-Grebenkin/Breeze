using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Docker.ViewModels;

/// <summary>
/// Panel refresh on request and, while the panel is visible, periodically. The next periodic refresh starts
/// <c>interval</c> after the previous one ends, so a slow docker builds no queue; requests during a refresh merge into
/// one repeat. The timer comes from <see cref="TimeProvider"/> (manual time in tests) and its ticks are posted to the UI
/// thread; everything else is called on the UI thread.
/// </summary>
public sealed class RefreshLoop : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly ITimer _timer;
    private readonly CancellationTokenSource _lifetime = new();
    private Task _running = Task.CompletedTask;
    private bool _again;
    private bool _disposed;

    public RefreshLoop(TimeSpan interval, TimeProvider time, IUiDispatcher dispatcher, Func<CancellationToken, Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _interval = interval;
        _refresh = refresh;
        _timer = time.CreateTimer(_ => dispatcher.Post(OnTick), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The panel is visible and refreshes periodically.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The panel was shown: refresh now and then periodically.</summary>
    public Task ActivateAsync()
    {
        if (IsActive)
        {
            return _running;
        }

        IsActive = true;
        return RefreshAsync();
    }

    /// <summary>The panel was hidden: periodic refresh stops; a running one completes.</summary>
    public void Deactivate()
    {
        IsActive = false;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Refreshes now; during a refresh, once more after it. The task completes when state is fresh.</summary>
    public Task RefreshAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        if (!_running.IsCompleted)
        {
            _again = true;
            return _running;
        }

        _running = RunAsync();
        return _running;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsActive = false;
        _lifetime.Cancel();
        _timer.Dispose();
        _lifetime.Dispose();
    }

    private async Task RunAsync()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        try
        {
            do
            {
                _again = false;
                await _refresh(_lifetime.Token);
            }
            while (_again);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return;
        }

        if (IsActive)
        {
            _timer.Change(_interval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTick()
    {
        if (IsActive)
        {
            _ = RefreshAsync();
        }
    }
}
