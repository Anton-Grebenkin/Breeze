using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Modules.Docker.Commands;
using CodeEditor.Modules.Docker.Services.Cli;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Panel actions are commands acting on the argument node, the node selected on the visible panel, or a palette pick.
/// docker gets safe arguments, removal needs confirmation, state is re-read after an action, and docker errors show on
/// the panel and in the status bar.
/// </summary>
public sealed class DockerPanelCommandsTests : IDisposable
{
    private readonly PanelFixture _fixture = new PanelFixture().WithTypicalState();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Stop_RunsDockerByContainerId_ThenRefreshes()
    {
        await _fixture.ShowAsync();
        var api = _fixture.ContainerNode("app-api-1");
        var hold = _fixture.Docker.Hold("stop");

        var stopping = _fixture.RunAsync(DockerCommandIds.Stop, api);
        Assert.Equal(("останавливается…", "Docker: остановка app-api-1…"), (api.Detail, _fixture.StatusBar.Message));
        hold.SetResult();
        await stopping;
        await _fixture.Panel.RefreshAsync();

        var stop = _fixture.Docker.Requests.Single(request => request.Arguments[0] == "stop");
        Assert.Equal(["stop", "a1"], stop.Arguments);
        Assert.Equal(DockerRunner.Timeout, stop.Timeout);
        Assert.False(api.IsBusy);
        Assert.Equal("Docker: остановка app-api-1 — готово.", _fixture.StatusBar.Message);
        Assert.True(_fixture.Docker.Count("ps") >= 2);
        Assert.Contains("> docker stop a1", _fixture.Output.GetOrCreate("Docker").Snapshot(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DockerError_IsShownOnThePanel()
    {
        await _fixture.ShowAsync();
        _fixture.Docker.Answer("start", "Error response from daemon: driver failed programming external connectivity\nmore", exitCode: 1);

        await _fixture.RunAsync(DockerCommandIds.Start, _fixture.ContainerNode("app-db-1"));

        const string Expected = "Docker: запуск app-db-1 — ошибка: Error response from daemon: driver failed programming external connectivity";
        Assert.Equal((Expected, Expected), (_fixture.Activity.Error, _fixture.StatusBar.Message));
        _fixture.Activity.DismissCommand.Execute(null);
        Assert.Null(_fixture.Activity.Error);
    }

    [Fact]
    public async Task Remove_AsksFirst_RunningContainerIsForced()
    {
        await _fixture.ShowAsync();
        _fixture.Dialogs.ConfirmAnswer = false;

        await _fixture.RunAsync(DockerCommandIds.Remove, _fixture.ContainerNode("app-api-1"));
        _fixture.Dialogs.ConfirmAnswer = true;
        await _fixture.RunAsync(DockerCommandIds.Remove, _fixture.ContainerNode("app-api-1"));
        await _fixture.RunAsync(DockerCommandIds.Remove, _fixture.ContainerNode("app-db-1"));

        Assert.Equal(["Контейнер «app-api-1» работает. Остановить и удалить его?", "Контейнер «app-api-1» работает. Остановить и удалить его?", "Удалить контейнер «app-db-1»?"],
            _fixture.Dialogs.Confirmations);
        Assert.Equal([["rm", "--force", "a1"], ["rm", "d1"]], _fixture.Docker.Requests.Where(request => request.Arguments[0] == "rm").Select(request => request.Arguments));
    }

    [Fact]
    public async Task RemoveImage_AsksFirst_ByNameAndTag()
    {
        await _fixture.ShowAsync();

        await _fixture.RunAsync(DockerCommandIds.RemoveImage, _fixture.Panel.Tree.AllImages.First());

        Assert.Equal("Удалить образ «postgres:17»?", Assert.Single(_fixture.Dialogs.Confirmations));
        Assert.Equal(["image", "rm", "postgres:17"], _fixture.Docker.Requests.Single(request => request.Arguments[0] == "image").Arguments);
    }

    [Fact]
    public async Task Compose_UpDownAndRestart_UseTheFileFromTheFolder()
    {
        _fixture.OpenFolderWithCompose("deploy/compose.yaml");
        await _fixture.ShowAsync();
        var file = Assert.Single(_fixture.Panel.Tree.ComposeFiles);
        var api = _fixture.Panel.Tree.Services.First();

        await _fixture.RunAsync(DockerCommandIds.ComposeUp, file);
        await _fixture.RunAsync(DockerCommandIds.ComposeUpBuild, api);
        await _fixture.RunAsync(DockerCommandIds.ComposeRestart, api);
        await _fixture.RunAsync(DockerCommandIds.ComposeDown, file);

        Assert.Equal(
        [
            ["compose", "--file=deploy/compose.yaml", "up", "--detach"],
            ["compose", "--file=deploy/compose.yaml", "up", "--detach", "--build", "api"],
            ["compose", "--file=deploy/compose.yaml", "restart", "api"],
            ["compose", "--file=deploy/compose.yaml", "down"],
        ],
        _fixture.Docker.Requests.Where(request => request.Arguments[0] == "compose" && request.Arguments[2] != "config").Select(request => request.Arguments));
        Assert.All(_fixture.Docker.Requests.Where(request => request.Arguments[0] == "compose"), request => Assert.Equal(PanelFixture.Root, request.WorkingDirectory));
        Assert.Equal(DockerRunner.LongTimeout, _fixture.Docker.Requests.First(request => request.Arguments is ["compose", _, "up", ..]).Timeout);
    }

    [Fact]
    public async Task WithoutTarget_ThePaletteOffersOnlySuitableContainers()
    {
        var status = await _fixture.RunAsync(DockerCommandIds.Stop);

        Assert.Equal(CommandExecutionStatus.Succeeded, status);
        Assert.Equal(["app-api-1", "cache"], _fixture.QuickPick.Items.Select(item => item.Title));
        await _fixture.QuickPick.PickAsync("cache");
        Assert.Equal(["stop", "c1"], _fixture.Docker.Requests.Single(request => request.Arguments[0] == "stop").Arguments);
    }

    [Fact]
    public async Task WithoutTarget_TheVisiblePanelsSelectionIsUsed()
    {
        await _fixture.ShowAsync();
        _fixture.Panel.Selected = _fixture.ContainerNode("app-db-1");

        await _fixture.RunAsync(DockerCommandIds.Start);

        Assert.Null(_fixture.QuickPick.Shown);
        Assert.Equal(["start", "d1"], _fixture.Docker.Requests.Single(request => request.Arguments[0] == "start").Arguments);
    }

    [Fact]
    public async Task UnavailableDocker_PaletteSaysWhy()
    {
        _fixture.Docker.Answer("ps", "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?", exitCode: 1);

        await _fixture.RunAsync(DockerCommandIds.Logs);

        Assert.Null(_fixture.QuickPick.Shown);
        Assert.Equal("Движок Docker не запущен. Запустите Docker Desktop и обновите панель.", _fixture.Activity.Error);
    }

    [Fact]
    public async Task CopyId_OpenInBrowser_UseTheShell()
    {
        await _fixture.ShowAsync();
        var api = _fixture.ContainerNode("app-api-1");

        await _fixture.RunAsync(DockerCommandIds.CopyId, api);
        await _fixture.RunAsync(DockerCommandIds.OpenInBrowser, api);

        Assert.Equal(("a1", "Docker: ID скопирован."), (_fixture.SystemShell.Clipboard, _fixture.StatusBar.Message));
        Assert.Equal("http://localhost:8080/", _fixture.SystemShell.Opened?.AbsoluteUri);
    }

    [Fact]
    public async Task OpenInBrowser_ContainerWithSeveralPorts_AsksWhichOne()
    {
        _fixture.Docker.Answer("ps", PanelFixture.Container("s1", "storage", "minio", "running", "Up 1 hour", "0.0.0.0:9000-9001->9000-9001/tcp"));
        await _fixture.ShowAsync();

        await _fixture.RunAsync(DockerCommandIds.OpenInBrowser, _fixture.ContainerNode("storage"));
        await _fixture.QuickPick.PickAsync(":9001");

        Assert.Equal("http://localhost:9001/", _fixture.SystemShell.Opened?.AbsoluteUri);
    }

    [Fact]
    public void TreeKeys_WorkOnlyInTheFocusedTree()
    {
        var resolver = new KeybindingResolver(_fixture.Keybindings);
        var delete = KeySequence.Parse("Delete").First;

        _fixture.Context.Set("dockerItem", "container");
        Assert.Equal(KeyResolutionKind.NotHandled, resolver.Resolve(delete, _fixture.Context).Kind);

        _fixture.Context.Set("dockerFocus", true);
        Assert.Equal(DockerCommandIds.Remove, resolver.Resolve(delete, _fixture.Context).Binding?.CommandId);

        _fixture.Context.Set("dockerItem", "image");
        Assert.Equal(DockerCommandIds.RemoveImage, resolver.Resolve(delete, _fixture.Context).Binding?.CommandId);
        Assert.Equal(DockerCommandIds.Inspect, resolver.Resolve(KeySequence.Parse("Enter").First, _fixture.Context).Binding?.CommandId);
    }
}
