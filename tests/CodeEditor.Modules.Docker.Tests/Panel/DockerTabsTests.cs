using CodeEditor.Core.Threading;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.ViewModels.Tabs;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Logs and inspect are editor tabs by id; a repeated command activates the open tab. Logs: the last lines, then a
/// stream without control characters, batched to the UI thread, within the line limit; closing the tab stops docker.
/// Inspect: JSON with hidden secrets.
/// </summary>
public sealed class DockerTabsTests : IDisposable
{
    private readonly PanelFixture _fixture = new PanelFixture().WithTypicalState();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Logs_OpenOneTabPerContainer_AndFollowTheOutput()
    {
        await _fixture.ShowAsync();
        var hold = _fixture.Docker.Hold("logs");
        _fixture.Docker.Answer("logs", "\u001b[32minfo\u001b[0m: started\nlistening on :80");
        var api = _fixture.ContainerNode("app-api-1");

        await _fixture.RunAsync(DockerCommandIds.Logs, api);
        await _fixture.RunAsync(DockerCommandIds.Logs, api);
        var log = Assert.IsType<ContainerLogViewModel>(_fixture.EditorViews.Content("docker.logs:app-api-1"));
        _fixture.Docker.HeldOutput!("GET / 200");

        var request = _fixture.Docker.Requests.Single(candidate => candidate.Arguments[0] == "logs");
        Assert.Equal(["logs", "--tail=500", "--follow", "app-api-1"], request.Arguments);
        Assert.Equal(Timeout.InfiniteTimeSpan, request.Timeout);
        Assert.Equal(["app-api-1 (журнал)", "app-api-1 (журнал)"], _fixture.EditorViews.Requests.Select(view => view.Title));
        Assert.Equal(["info: started", "listening on :80", "GET / 200"], log.Lines);
        Assert.Equal((LogState.Live, "Новые строки — по мере вывода"), (log.State, log.Status));

        _fixture.EditorViews.Close("docker.logs:app-api-1");
        await log.Completion;
        _fixture.Docker.HeldOutput!("after close");
        Assert.Equal(3, log.LineCount);
        hold.SetResult();
    }

    [Fact]
    public async Task Logs_EndWithTheContainer_AndCanReconnect()
    {
        _fixture.Docker.Answer("logs", "bye");
        var log = new ContainerLogViewModel("api", _fixture.Runner, new InlineUiDispatcher());
        var changes = new List<LogLinesEventArgs>();
        log.Changed += (_, e) => changes.Add(e);

        log.Start();
        await log.Completion;
        log.StartCommand.Execute(null);
        await log.Completion;

        Assert.Equal((LogState.Ended, "Поток закончился: контейнер остановлен."), (log.State, log.Status));
        Assert.Equal(["bye"], log.Lines);
        Assert.Equal([true, false, true, false], changes.Select(change => change.Reset));
        Assert.Equal(2, _fixture.Docker.Count("logs"));
    }

    [Fact]
    public async Task Logs_DockerError_IsTheStatus()
    {
        _fixture.Docker.Answer("logs", "Error response from daemon: No such container: api", exitCode: 1);
        using var log = new ContainerLogViewModel("api", _fixture.Runner, new InlineUiDispatcher());

        log.Start();
        await log.Completion;

        Assert.Equal((LogState.Failed, "Журнал недоступен: Error response from daemon: No such container: api"), (log.State, log.Status));
    }

    [Fact]
    public void Logs_KeepOnlyTheLastLines_InOneUpdatePerBatch()
    {
        var dispatcher = new QueueDispatcher();
        var lines = Enumerable.Range(1, ContainerLogViewModel.MaxLines + 5).Select(number => "line " + number);
        _fixture.Docker.Answer("logs", string.Join('\n', lines));
        _fixture.Docker.Hold("logs");
        using var log = new ContainerLogViewModel("api", _fixture.Runner, dispatcher);
        var changes = new List<LogLinesEventArgs>();
        log.Changed += (_, e) => changes.Add(e);

        log.Start();
        dispatcher.RunAll();

        var update = Assert.Single(changes, change => !change.Reset);
        Assert.Equal((ContainerLogViewModel.MaxLines + 5, 5), (update.Added.Count, update.Removed));
        Assert.Equal((ContainerLogViewModel.MaxLines, "line 6"), (log.LineCount, log.Lines[0]));
        Assert.Equal(1, dispatcher.Posted);
    }

    [Fact]
    public async Task Inspect_HidesSecrets_AndCopiesTheText()
    {
        await _fixture.ShowAsync();
        _fixture.Docker.Answer("container inspect", """[{"Config": {"Env": ["POSTGRES_PASSWORD=s3cret", "PGDATA=/data"]}}]""");

        await _fixture.RunAsync(DockerCommandIds.Inspect, _fixture.ContainerNode("app-db-1"));
        var inspect = Assert.IsType<InspectViewModel>(_fixture.EditorViews.Content("docker.inspect:app-db-1"));
        await inspect.RefreshCommand.ExecuteAsync(null);
        inspect.CopyCommand.Execute(null);

        Assert.Equal(["container", "inspect", "d1"], _fixture.Docker.Requests.First(request => request.Arguments[0] == "container").Arguments);
        Assert.Contains("POSTGRES_PASSWORD=***", inspect.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cret", inspect.Text, StringComparison.Ordinal);
        Assert.Equal(inspect.Text, _fixture.SystemShell.Clipboard);
        Assert.Equal("app-db-1 (сведения)", _fixture.EditorViews.Requests.Single().Title);
    }

    [Fact]
    public async Task InspectImage_ByReference_ErrorIsShown()
    {
        await _fixture.ShowAsync();
        _fixture.Docker.Answer("image inspect", "Error: No such image: postgres:17", exitCode: 1);

        await _fixture.RunAsync(DockerCommandIds.Inspect, _fixture.Panel.Tree.AllImages.First());
        var inspect = Assert.IsType<InspectViewModel>(_fixture.EditorViews.Content("docker.inspect:image:postgres:17"));
        await inspect.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(("Error: No such image: postgres:17", false), (inspect.Error, inspect.CopyCommand.CanExecute(null)));
    }

    /// <summary>The test's UI thread: actions queue up until the test runs them.</summary>
    private sealed class QueueDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _actions = new();

        public int Posted { get; private set; }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public void Post(Action action)
        {
            Posted++;
            _actions.Enqueue(action);
        }

        public void RunAll()
        {
            while (_actions.TryDequeue(out var action))
            {
                action();
            }
        }
    }
}
