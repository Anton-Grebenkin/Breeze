using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Docker.Services;
using CodeEditor.Modules.Docker.Services.Agent;
using CodeEditor.Modules.Docker.Services.Cli;
using CodeEditor.Modules.Docker.Services.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Docker.Tests;

/// <summary>
/// Real docker, read-only: it accepts the table formats, a compose project without containers is an empty table, an
/// unknown object is a docker error for the model, and the panel's line-delimited JSON parses. The test never changes
/// the machine's containers or images. Skipped without docker or a running engine: it is about the output format.
/// </summary>
public sealed class DockerCliTests : IDisposable
{
    private static readonly TimeSpan EngineCheckTimeout = TimeSpan.FromSeconds(30);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "CodeEditor.DockerTests", Guid.NewGuid().ToString("N"));
    private readonly PhysicalFileSystem _files = new();
    private readonly ProcessRunner _runner = new();
    private readonly Workspace _workspace;
    private readonly DockerAgentTools _tools;

    public DockerCliTests()
    {
        Directory.CreateDirectory(_root);
        _workspace = new Workspace(_files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(_root);
        _tools = new DockerAgentTools(new DockerRunner(_runner, _workspace, _files), _workspace, _files, new PassThroughOutputStore());
    }

    public void Dispose()
    {
        _workspace.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Tables_HaveTheirHeaders()
    {
        await SkipWithoutDockerAsync();

        var containers = await InvokeAsync(new() { ["action"] = "ps" });
        var images = await InvokeAsync(new() { ["action"] = "images" });

        Assert.StartsWith("NAMES", containers, StringComparison.Ordinal);
        Assert.StartsWith("REPOSITORY:TAG", images, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComposeProjectWithoutContainers_IsAnEmptyTable()
    {
        await SkipWithoutDockerAsync();
        await File.WriteAllTextAsync(Path.Combine(_root, "compose.yaml"), "services:\n  web:\n    image: nginx:alpine\n", TestContext.Current.CancellationToken);

        var services = await InvokeAsync(new() { ["action"] = "compose_ps" });

        Assert.StartsWith("NAME", services, StringComparison.Ordinal);
        Assert.DoesNotContain("web", services, StringComparison.Ordinal);
    }

    // Panel (ADR 0033): docker accepts the JSON templates and the parser understands its output. Only ps and images run.
    [Fact]
    public async Task PanelReader_ParsesTheRealOutput()
    {
        await SkipWithoutDockerAsync();
        var docker = new DockerRunner(_runner, _workspace, _files);
        var reader = new DockerStateReader(docker, new ComposeProjects(docker, _files), _workspace, new Panel.StaticFileIndex());

        var snapshot = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DockerAvailability.Available, snapshot.Availability);
        Assert.All(snapshot.Containers, container => Assert.False(string.IsNullOrEmpty(container.Id) || string.IsNullOrEmpty(container.Name)));
        Assert.All(snapshot.Containers, container => Assert.NotEqual(ContainerState.Unknown, container.State));
        Assert.All(snapshot.Images, image => Assert.False(string.IsNullOrEmpty(image.Id)));
        Assert.Empty(snapshot.Compose);
    }

    [Fact]
    public async Task UnknownObject_IsADockerErrorForTheModel()
    {
        await SkipWithoutDockerAsync();

        var error = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["action"] = "inspect", ["name"] = "codeeditor-no-such-object" }));

        Assert.Contains("codeeditor-no-such-object", error.Message, StringComparison.Ordinal);
    }

    private async Task SkipWithoutDockerAsync()
    {
        if (!_tools.CreateTools().Any())
        {
            Assert.Skip("docker is not installed.");
        }

        var request = new ProcessRequest(DockerRunner.Executable, ["version", "--format", "{{.Server.Version}}"], _root) { Timeout = EngineCheckTimeout };
        if ((await _runner.RunAsync(request, onLine: null, TestContext.Current.CancellationToken)).ExitCode != 0)
        {
            Assert.Skip("Docker engine is not running.");
        }
    }

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments)
    {
        var tool = _tools.CreateTools().OfType<AIFunction>().Single(candidate => candidate.Name == DockerAgentTools.ReadName);
        return (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }

    /// <summary>Returns long output as is; output files are tested in the agent module.</summary>
    private sealed class PassThroughOutputStore : IAgentOutputStore
    {
        public string Fit(string text, string toolName) => text;
    }
}
