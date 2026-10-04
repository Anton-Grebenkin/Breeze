using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Tests.Panel;

/// <summary>
/// Reading state for the panel: no docker means no processes; a non-responding engine gives a clear reason; a compose
/// project is read by docker once until the file changes; the environment matches the user's terminal.
/// </summary>
public sealed class DockerStateReaderTests : IDisposable
{
    private readonly PanelFixture _fixture = new PanelFixture().WithTypicalState();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task WithoutDocker_NoProcessRuns()
    {
        using var fixture = new PanelFixture(dockerInstalled: false);

        await fixture.ShowAsync();

        Assert.Equal((DockerAvailability.NotInstalled, true), (fixture.Panel.Availability, fixture.Panel.IsUnavailable));
        Assert.Contains("Docker Desktop", fixture.Panel.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Docker.Requests);
    }

    [Fact]
    public async Task EngineNotRunning_IsRecognized()
    {
        _fixture.Docker.Answer("ps", "error during connect: Get \"http://%2F%2F.%2Fpipe%2FdockerDesktopLinuxEngine/v1.47/containers/json\": open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified.", exitCode: 1);

        await _fixture.ShowAsync();

        Assert.Equal(DockerAvailability.EngineStopped, _fixture.Panel.Availability);
        Assert.Equal("Движок Docker не запущен. Запустите Docker Desktop и обновите панель.", _fixture.Panel.Message);
    }

    [Fact]
    public async Task OtherDockerError_IsShownByItsFirstLine()
    {
        _fixture.Docker.Answer("ps", "permission denied while trying to connect\nmore details", exitCode: 1);

        await _fixture.ShowAsync();

        Assert.Equal(DockerAvailability.Failed, _fixture.Panel.Availability);
        Assert.Equal("Docker ответил ошибкой: permission denied while trying to connect", _fixture.Panel.Message);
    }

    [Fact]
    public async Task Panel_RunsDockerWithTheUsersEnvironment_InTheFolder()
    {
        _fixture.OpenFolderWithCompose();

        await _fixture.ShowAsync();

        var request = _fixture.Docker.Requests[0];
        Assert.Equal((PanelFixture.Root, false), (request.WorkingDirectory, request.HideSecretVariables));
        Assert.Equal("false", request.Environment["DOCKER_CLI_HINTS"]);
        Assert.Equal(DockerQueries.Containers, request.Arguments);
    }

    [Fact]
    public async Task WithoutFolder_DockerRunsInTheUserFolder_AndComposeIsEmpty()
    {
        await _fixture.ShowAsync();

        Assert.NotEqual(PanelFixture.Root, _fixture.Docker.Requests[0].WorkingDirectory);
        Assert.Equal(0, _fixture.Docker.Count("compose config"));
        Assert.IsType<DockerHintViewModel>(Assert.Single(_fixture.Panel.Tree.Compose.Children));
    }

    [Fact]
    public async Task ComposeProject_IsReadOnce_UntilTheFileChanges()
    {
        _fixture.OpenFolderWithCompose();

        await _fixture.ShowAsync();
        await _fixture.Panel.RefreshAsync();
        _fixture.Files.Touch(Path.Combine(PanelFixture.Root, "compose.yaml"));
        await _fixture.Panel.RefreshAsync();

        Assert.Equal(3, _fixture.Docker.Count("ps"));
        Assert.Equal(2, _fixture.Docker.Count("compose config"));
        Assert.Equal(["compose", "--file=compose.yaml", "config", "--format", "json"], _fixture.Docker.Requests.First(request => request.Arguments[0] == "compose").Arguments);
    }
}
