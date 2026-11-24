using System.Globalization;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// A model request waiting to be repeated after a service failure (<see cref="RequestRetries"/>): one feed row with
/// a countdown, so a pause of half a minute does not look like a hang. When the pause ends the row says the request was
/// repeated; the turn's next failure reuses the row.
/// </summary>
internal sealed class RetryRow : IDisposable
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(1);

    private readonly RequestRetries _retries;
    private readonly Func<string, ChatMessageViewModel> _addRow;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private ChatMessageViewModel? _row;
    private RequestRetry? _retry;
    private ITimer? _timer;
    private DateTimeOffset _until;
    private bool _disposed;

    /// <param name="addRow">Adds a status row with the given text to the turn's feed.</param>
    public RetryRow(RequestRetries retries, Func<string, ChatMessageViewModel> addRow, IUiDispatcher dispatcher, TimeProvider time)
    {
        _retries = retries;
        _addRow = addRow;
        _dispatcher = dispatcher;
        _time = time;
        _retries.Waiting += OnWaiting;
    }

    /// <summary>Updates the countdown; on the UI thread.</summary>
    public void Refresh()
    {
        if (_disposed || _row is null || _retry is not { } retry)
        {
            return;
        }

        var left = _until - _time.GetUtcNow();
        if (left > TimeSpan.Zero)
        {
            _row.Text = Format(Strings.RetryWaiting, (int)Math.Ceiling(left.TotalSeconds), retry.Attempt, retry.Attempts, retry.Reason);
            return;
        }

        _row.Text = Format(Strings.RetryRepeated, retry.Attempt, retry.Attempts, retry.Reason);
        StopTimer();
    }

    public void Dispose()
    {
        _retries.Waiting -= OnWaiting;

        // Stopped mid-pause: the row keeps what happened, without a countdown that no longer runs.
        if (_timer is not null && _row is not null && _retry is not null)
        {
            _row.Text = _retry.Reason;
        }

        _disposed = true;
        StopTimer();
    }

    private void OnWaiting(object? sender, RequestRetry retry) => _dispatcher.Post(() => Show(retry));

    private void Show(RequestRetry retry)
    {
        if (_disposed)
        {
            return;
        }

        StopTimer();
        _retry = retry;
        _until = _time.GetUtcNow() + retry.Delay;
        _row ??= _addRow(string.Empty);
        _timer = _time.CreateTimer(_ => _dispatcher.Post(Refresh), null, Period, Period);
        Refresh();
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
