using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// Progress of slow tool calls in the feed while a turn runs (<see cref="RunningToolCalls"/>): twice a second the rows
/// of calls running longer than <see cref="ShowAfter"/> show the elapsed time, the last output line and, for a tool
/// waiting for a process, "To background". Polling instead of an event per line: a build prints thousands of lines,
/// the row shows one.
/// </summary>
internal sealed class LiveToolRows : IDisposable
{
    public static readonly TimeSpan Period = TimeSpan.FromMilliseconds(500);

    /// <summary>Quick calls finish before their row would flicker with progress.</summary>
    public static readonly TimeSpan ShowAfter = TimeSpan.FromSeconds(2);

    private readonly RunningToolCalls _calls;
    private readonly Func<string, ToolRowViewModel?> _rowOf;
    private readonly ITimer _timer;
    private HashSet<ToolRowViewModel> _shown = [];
    private bool _disposed;

    /// <param name="rowOf">The feed row of a call id; <c>null</c> until the feed shows the call.</param>
    public LiveToolRows(RunningToolCalls calls, Func<string, ToolRowViewModel?> rowOf, TimeProvider time, IUiDispatcher dispatcher)
    {
        _calls = calls;
        _rowOf = rowOf;
        _timer = time.CreateTimer(_ => dispatcher.Post(Refresh), null, Period, Period);
    }

    /// <summary>Updates the rows; on the UI thread.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var shown = new HashSet<ToolRowViewModel>();
        foreach (var call in _calls.Snapshot())
        {
            if (call.Elapsed < ShowAfter || _rowOf(call.CallId) is not { } row)
            {
                continue;
            }

            var id = call.CallId;
            row.ShowProgress(DurationText.Format(call.Elapsed), call.LastLine, call.OffersBackground ? () => _calls.MoveToBackground(id) : null);
            shown.Add(row);
        }

        foreach (var row in _shown.Where(row => !shown.Contains(row)))
        {
            row.EndProgress();
        }

        _shown = shown;
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        foreach (var row in _shown)
        {
            row.EndProgress();
        }

        _shown.Clear();
    }
}
