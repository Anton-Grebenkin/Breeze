using System.Text;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// An agent command running in the background (ADR 0012): a server, watcher or long run. Output is kept per line
/// (oldest dropped beyond <see cref="MaxLines"/>); the model reads only what is new since the last read and can wait
/// for a line matching a regex, like Cursor's background terminals. Lines arrive on process reader threads.
/// </summary>
public sealed class BackgroundCommand(int id, string command, string folder, DateTimeOffset started) : IDisposable
{
    /// <summary>Last lines kept in memory; the full output is in the Commands channel.</summary>
    public const int MaxLines = 20_000;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _dropped;
    private int _read;

    public int Id { get; } = id;

    public string Command { get; } = command;

    public string Folder { get; } = folder;

    public DateTimeOffset Started { get; } = started;

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>Exit code; <c>null</c> while running or if stopped.</summary>
    public int? ExitCode { get; private set; }

    public bool IsRunning { get; private set; } = true;

    /// <summary>Stopped by <c>stop_command</c> or a workspace change.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>Finish time; <c>null</c> while running.</summary>
    public DateTimeOffset? Finished { get; private set; }

    public void Append(string line)
    {
        lock (_gate)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
                _dropped++;
                _read = Math.Max(0, _read - 1);
            }
        }

        Signal();
    }

    public void Finish(int? exitCode, bool stopped, DateTimeOffset at)
    {
        ExitCode = exitCode;
        IsStopped = stopped;
        Finished = at;
        IsRunning = false;
        Signal();
    }

    /// <summary>Lines added since the last read; the next read starts after them.</summary>
    public string ReadNew()
    {
        lock (_gate)
        {
            var text = new StringBuilder();
            foreach (var line in _lines.Skip(_read))
            {
                text.Append(line).Append('\n');
            }

            _read = _lines.Count;
            return text.ToString();
        }
    }

    /// <summary>Lines dropped from memory since the start.</summary>
    public int Dropped
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    /// <summary>
    /// Waits for an unread line matching <paramref name="pattern"/> (if given), the command's exit, or the timeout.
    /// Doesn't advance the read position: <see cref="ReadNew"/> builds the reply.
    /// </summary>
    /// <returns><c>true</c> if a line matched the pattern.</returns>
    public async Task<bool> WaitAsync(string? pattern, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var regex = pattern is null ? null : new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        var scanned = 0;
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                scanned = Math.Max(scanned, _read);
                if (regex is not null && FindMatch(regex, ref scanned))
                {
                    return true;
                }

                changed = _changed.Task;
            }

            if (!IsRunning)
            {
                return false;
            }

            try
            {
                await changed.WaitAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }
    }

    public void Dispose() => Cancellation.Dispose();

    private bool FindMatch(Regex regex, ref int scanned)
    {
        for (; scanned < _lines.Count; scanned++)
        {
            if (regex.IsMatch(_lines[scanned]))
            {
                return true;
            }
        }

        return false;
    }

    private void Signal()
    {
        TaskCompletionSource previous;
        lock (_gate)
        {
            previous = _changed;
            _changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        previous.TrySetResult();
    }
}
