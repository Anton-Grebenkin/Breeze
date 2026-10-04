using CodeEditor.Core.Threading;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.ViewModels.Tree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Docker.ViewModels.Tabs;

/// <summary>
/// Container logs in an editor tab (ADR 0033): the last <see cref="DockerQueries.LogTail"/> lines, then a live stream
/// (<c>docker logs --follow</c>) without terminal control sequences. Lines arrive on process threads and reach the UI
/// thread in batches (<see cref="LineBatch"/>). At most <see cref="MaxLines"/> last lines stay in memory. Closing the tab
/// stops docker; when the container stops the stream ends, and "Reconnect" reads again.
/// </summary>
public sealed partial class ContainerLogViewModel : ObservableObject, IDisposable
{
    public const int MaxLines = 10_000;

    private readonly DockerRunner _docker;
    private readonly IUiDispatcher _dispatcher;
    private readonly Queue<string> _lines = new();
    private CancellationTokenSource? _stream;
    private LineBatch? _batch;

    public ContainerLogViewModel(string container, DockerRunner docker, IUiDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(container);
        Container = container;
        _docker = docker;
        _dispatcher = dispatcher;
    }

    public string Container { get; }

    [ObservableProperty]
    public partial LogState State { get; private set; }

    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Scroll to new lines; the view turns it off when the user scrolls up.</summary>
    [ObservableProperty]
    public partial bool AutoScroll { get; set; } = true;

    [ObservableProperty]
    public partial bool WordWrap { get; set; }

    public int LineCount => _lines.Count;

    /// <summary>A copy of the current lines, for a view created after the stream started.</summary>
    public IReadOnlyList<string> Lines => [.. _lines];

    /// <summary>The log read; ends with docker. Tests await it.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>Lines were added or the log was cleared; raised on the UI thread.</summary>
    public event EventHandler<LogLinesEventArgs>? Changed;

    /// <summary>Reads again: the last lines, then the live stream. The previous stream stops.</summary>
    [RelayCommand]
    public void Start()
    {
        Stop();
        Reset();
        var stream = new CancellationTokenSource();
        var batch = new LineBatch(_dispatcher, MaxLines, Append);
        (_stream, _batch) = (stream, batch);
        State = LogState.Live;
        Status = Strings.LogLive;
        Completion = FollowAsync(batch, stream.Token);
    }

    /// <summary>Clears the tab; the stream continues.</summary>
    [RelayCommand]
    public void Clear() => Reset();

    public void Dispose() => Stop();

    private async Task FollowAsync(LineBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _docker.RunForPanelAsync(
                DockerQueries.Logs(Container, follow: true), Timeout.InfiniteTimeSpan, line => batch.Add(TerminalEscapes.Strip(line)), cancellationToken);
            if (!ReferenceEquals(batch, _batch))
            {
                return;
            }

            batch.Flush();
            (State, Status) = result.ExitCode == 0
                ? (LogState.Ended, Strings.LogEnded)
                : (LogState.Failed, DockerNodeText.Format(Strings.LogFailed, DockerErrors.FirstLine(result.Output)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tab was closed or the stream restarted.
        }
        catch (InvalidOperationException exception)
        {
            (State, Status) = (LogState.Failed, DockerNodeText.Format(Strings.LogFailed, exception.Message));
        }
    }

    private void Append(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            _lines.Enqueue(line);
        }

        var removed = 0;
        for (; _lines.Count > MaxLines; removed++)
        {
            _lines.Dequeue();
        }

        OnPropertyChanged(nameof(LineCount));
        Changed?.Invoke(this, new LogLinesEventArgs(lines, removed, reset: false));
    }

    private void Reset()
    {
        _lines.Clear();
        OnPropertyChanged(nameof(LineCount));
        Changed?.Invoke(this, new LogLinesEventArgs([], 0, reset: true));
    }

    private void Stop()
    {
        _batch?.Close();
        _stream?.Cancel();
        _stream?.Dispose();
        (_stream, _batch) = (null, null);
    }
}
