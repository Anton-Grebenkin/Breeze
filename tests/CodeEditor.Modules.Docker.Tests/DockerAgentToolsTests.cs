using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Docker.Services.Agent;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Docker.Tests;

/// <summary>
/// Docker tools over a fake process runner: docker runs without a shell in the root, there are no tools without docker,
/// compose uses a file from the root, changes go through a card with commands, secrets and control characters never
/// reach the model, docker errors do.
/// </summary>
public sealed class DockerAgentToolsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\app");
    private static readonly string ToolsFolder = Path.GetFullPath(@"C:\tools");
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeFileSystem _files = new FakeFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Combine(Root, "compose.yaml"))
        .AddFile(Path.Combine(ToolsFolder, "docker.exe"));
    private readonly Workspace _workspace;
    private readonly DockerAgentTools _tools;

    public DockerAgentToolsTests()
    {
        _workspace = new Workspace(_files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(Root);
        _tools = Create(ToolsFolder);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task Read_RunsDockerInTheRoot_WithoutShellHintsAndSecrets()
    {
        _runner.Returns(0, "NAMES   IMAGE\napi     app:dev\n");

        var result = await InvokeAsync(DockerAgentTools.ReadName, new() { ["action"] = "ps" });

        Assert.Equal("NAMES   IMAGE\napi     app:dev", result);
        var request = Assert.Single(_runner.Requests);
        Assert.Equal(("docker", Root, DockerRunner.Timeout), (request.FileName, request.WorkingDirectory, request.Timeout));
        Assert.Equal("false", request.Environment["DOCKER_CLI_HINTS"]);
        Assert.True(request.HideSecretVariables);
    }

    // The model wastes no tokens or steps on tools that cannot work.
    [Fact]
    public void WithoutDockerOnPath_ThereAreNoTools() => Assert.Empty(Create(Path.GetFullPath(@"C:\empty")).CreateTools());

    [Fact]
    public async Task Compose_UsesTheFileFromTheRoot()
    {
        await InvokeAsync(DockerAgentTools.ReadName, new() { ["action"] = "compose_ps" });

        Assert.Equal(["compose", "--file=compose.yaml", "ps", "--all"], _runner.Requests[0].Arguments);
    }

    [Fact]
    public async Task Compose_WithoutAFileInTheRoot_IsRefusedBeforeDocker()
    {
        _files.DeleteFile(Path.Combine(Root, "compose.yaml"));

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(DockerAgentTools.ReadName, new() { ["action"] = "compose_logs" }));

        Assert.Contains("docker-compose.yml", error.Message, StringComparison.Ordinal);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task Logs_ComeWithoutTerminalEscapes()
    {
        _runner.Returns(0, "\u001b[32minfo\u001b[0m: started\n");

        var result = await InvokeAsync(DockerAgentTools.ReadName, new() { ["action"] = "logs", ["name"] = "api" });

        Assert.Equal("info: started", result);
    }

    [Fact]
    public async Task Inspect_HidesSecretVariables()
    {
        _runner.Returns(0, """[{"Config": {"Env": ["POSTGRES_PASSWORD=s3cret", "PGDATA=/data"]}}]""");

        var result = await InvokeAsync(DockerAgentTools.ReadName, new() { ["action"] = "inspect", ["name"] = "db" });

        Assert.DoesNotContain("s3cret", result, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD=***", result, StringComparison.Ordinal);
        Assert.Contains("PGDATA=/data", result, StringComparison.Ordinal);
    }

    // The card shows exactly what docker will do; a relative volume path resolves from the workspace root.
    [Fact]
    public async Task Run_PreviewShowsTheCommand_ThenItRuns()
    {
        Dictionary<string, object?> arguments = new() { ["action"] = "run", ["image"] = "postgres:17", ["name"] = "db", ["volumes"] = new[] { "./data:/var/lib/postgresql/data" } };

        var preview = Assert.Single(await _tools.PreviewAsync(DockerAgentTools.ChangeName, arguments, TestContext.Current.CancellationToken));
        _runner.Returns(0, "4f2a9c\n");
        var result = await InvokeAsync(DockerAgentTools.ChangeName, arguments);

        var volume = "--volume=" + Path.Combine(Root, "data") + ":/var/lib/postgresql/data";
        Assert.Equal((ProposedChangeKind.Command, "docker run --detach --name=db " + volume + " postgres:17"), (preview.Kind, preview.NewText));
        Assert.Equal(["run", "--detach", "--name=db", volume, "postgres:17"], _runner.Requests[^1].Arguments);
        Assert.Equal(DockerRunner.LongTimeout, _runner.Requests[^1].Timeout);
        Assert.Equal("4f2a9c", result);
    }

    [Fact]
    public async Task DockerError_GoesToTheModel()
    {
        _runner.Returns(1, "Error response from daemon: No such container: api");

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(DockerAgentTools.ChangeName, new() { ["action"] = "stop", ["name"] = "api" }));

        Assert.Contains("No such container", error.Message, StringComparison.Ordinal);
        Assert.Equal(DockerRunner.Timeout, _runner.Requests[0].Timeout);
    }

    [Fact]
    public async Task PathOutsideTheFolder_IsRefused() =>
        await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(DockerAgentTools.ChangeName, new() { ["action"] = "build", ["context"] = @"..\other" }));

    [Fact]
    public void ReadTool_IsReadOnly_ChangeToolNeedsApproval()
    {
        var tools = _tools.CreateTools().ToList();

        Assert.True(ReadOnlyAIFunction.IsReadOnly(tools.Single(tool => tool.Name == DockerAgentTools.ReadName)));
        Assert.NotNull(tools.Single(tool => tool.Name == DockerAgentTools.ChangeName).GetService<ApprovalRequiredAIFunction>());
    }

    private DockerAgentTools Create(string path) =>
        new(new DockerRunner(_runner, _workspace, _files, path, ".EXE"), _workspace, _files, new PassThroughOutputStore());

    private async Task<string> InvokeAsync(string name, Dictionary<string, object?> arguments)
    {
        var tool = _tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == name);
        return (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }

    /// <summary>Returns long output as is; output files are tested in the agent module.</summary>
    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }
}
