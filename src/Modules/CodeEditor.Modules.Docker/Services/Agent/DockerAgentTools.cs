using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Docker.Resources;
using CodeEditor.Modules.Docker.Services.Cli;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>
/// Agent tools for Docker (ADR 0028). <c>docker</c> only reads (containers, images, logs, details, compose services) and
/// runs without asking in every mode (<see cref="ReadOnlyAIFunction"/>). <c>docker_change</c> builds, starts and stops;
/// every call goes through a card listing the docker commands (<see cref="IAgentChangePreviewer"/>). No tools without
/// docker installed. Paths stay inside the workspace; compose always uses a file from it.
/// </summary>
public sealed class DockerAgentTools(DockerRunner docker, IWorkspace workspace, IFileSystem fileSystem, IAgentOutputStore outputs)
    : IAgentToolProvider, IAgentChangePreviewer
{
    public const string ReadName = "docker";
    public const string ChangeName = "docker_change";

    public IEnumerable<AITool> CreateTools() => docker.IsInstalled
        ?
        [
            new ReadOnlyAIFunction(AIFunctionFactory.Create(ReadAsync, ReadName,
                "Reads Docker without changing it: containers with their state and ports (ps), images, the output of a container (logs), " +
                "details of a container, image, volume or network as JSON (inspect), the services of the compose project in the workspace and their output " +
                "(compose_ps, compose_logs). Use it instead of run_command for Docker. Values of variables that look like secrets are hidden. " +
                "Long output is saved to a file: you get its start and end and the path.")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ChangeAsync, ChangeName,
                "Changes Docker; the user sees and approves each call. Actions: build (build an image from a Dockerfile), run (start a container from an image), " +
                "exec (run a command that finishes in a running container), start, stop, restart, rm (remove a stopped container; cannot be undone), " +
                "compose_up (start the compose project of the workspace in the background; build: true rebuilds images after code changes), " +
                "compose_down (stop and remove the project containers; volumes stay). After starting, check the state with docker ps or compose_ps " +
                "and read the logs of a container that exited.")),
        ]
        : [];

    public bool CanPreview(string toolName) => toolName == ChangeName;

    public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var change = Normalize(DockerArguments.Change(arguments));
        IReadOnlyList<FileChangePreview> preview =
        [
            new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, DockerCommands.Display(DockerCommands.Change(change)))
            {
                Title = Strings.ApprovalTitle,
                Header = Strings.ApprovalHeader,
            },
        ];
        return Task.FromResult(preview);
    }

    private async Task<string> ReadAsync(
        [Description("ps, images, logs, inspect, compose_ps or compose_logs.")] string action,
        [Description("logs: the container; inspect: a container, image, volume or network.")] string? name = null,
        [Description("logs, compose_logs: number of last lines, up to 2000; default 200.")] int tail = DockerCommands.DefaultTail,
        [Description("logs: only output since this time: 10m, 2h or 2026-10-03T10:00:00.")] string? since = null,
        [Description("compose_logs: only these services; empty — all.")] string[]? services = null,
        [Description("compose_ps, compose_logs: the compose file relative to the workspace root; default compose.yaml or docker-compose.yml in the root.")] string? file = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DockerRead(action, name, tail, since) { Services = services ?? [], ComposeFile = ComposeFile(action, file) };
        var output = await RunAsync(action, DockerCommands.Read(request), DockerRunner.Timeout, cancellationToken);
        if (action == DockerCommands.Inspect)
        {
            output = InspectSecrets.Hide(output);
        }

        return output.Length == 0 ? Strings.EmptyOutput : outputs.Fit(output, ReadName);
    }

    private async Task<string> ChangeAsync(
        [Description("build, run, exec, start, stop, restart, rm, compose_up or compose_down.")] string action,
        [Description("run: a name for the new container; exec, start, stop, restart, rm: the container.")] string? name = null,
        [Description("run: the image to start; build: the name:tag of the built image.")] string? image = null,
        [Description("run: the command and its arguments instead of the image command; exec: the command and its arguments (required), e.g. [\"dotnet\", \"ef\", \"database\", \"update\"].")] string[]? command = null,
        [Description("run: published ports host:container, e.g. 8080:80.")] string[]? ports = null,
        [Description("run: environment variables NAME=value.")] string[]? env = null,
        [Description("run: volumes name:/path or host-path:/path; a relative host path (./data) is relative to the workspace root.")] string[]? volumes = null,
        [Description("run: the network to connect the container to.")] string? network = null,
        [Description("run: true (default) — start in the background; false — wait for the command to finish, return its output and remove the container.")] bool detach = true,
        [Description("build: the build folder relative to the workspace root; default the root.")] string? context = null,
        [Description("build: the Dockerfile relative to the workspace root; default Dockerfile in the build folder.")] string? dockerfile = null,
        [Description("compose_up, compose_down: the compose file relative to the workspace root; default compose.yaml or docker-compose.yml in the root.")] string? file = null,
        [Description("compose_up: only these services; empty — all.")] string[]? services = null,
        [Description("compose_up: rebuild images before starting.")] bool build = false,
        CancellationToken cancellationToken = default)
    {
        var change = Normalize(new DockerChange(action)
        {
            Name = name, Image = image, Command = command ?? [], Ports = ports ?? [], Env = env ?? [], Volumes = volumes ?? [], Network = network,
            Detach = detach, Context = context, Dockerfile = dockerfile, ComposeFile = file, Services = services ?? [], Build = build,
        });
        var timeout = DockerCommands.IsLong(action) ? DockerRunner.LongTimeout : DockerRunner.Timeout;
        var output = await RunAsync(action, DockerCommands.Change(change), timeout, cancellationToken);
        return output.Length == 0 ? Strings.Done : outputs.Fit(output, ChangeName);
    }

    // The model's paths must be inside the workspace; docker gets them relative to the root with '/'.
    private DockerChange Normalize(DockerChange change) => change with
    {
        Context = RelativePath(change.Context),
        Dockerfile = RelativePath(change.Dockerfile),
        ComposeFile = ComposeFile(change.Action, change.ComposeFile),
        Volumes = [.. change.Volumes.Select(Volume)],
    };

    /// <exception cref="AgentToolException">docker failed, timed out or is not installed.</exception>
    private async Task<string> RunAsync(string action, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await docker.RunAsync(arguments, timeout, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new AgentToolException(exception.Message);
        }

        if (result.TimedOut)
        {
            throw new AgentToolException(Format(Strings.DockerTimedOut, action, (int)timeout.TotalSeconds));
        }

        var output = TerminalEscapes.Strip(result.Output).TrimEnd();
        return result.ExitCode == 0 ? output : throw new AgentToolException(Format(Strings.DockerFailed, action, result.ExitCode, output));
    }

    // An explicit file stops docker from searching parent folders; without a file in the root compose does not run.
    private string? ComposeFile(string action, string? file)
    {
        if (!DockerCommands.IsCompose(action))
        {
            return null;
        }

        if (RelativePath(file) is { } relative)
        {
            return relative;
        }

        var root = docker.Root;
        return DockerCommands.ComposeFiles.FirstOrDefault(candidate => fileSystem.FileExists(Path.Combine(root, candidate)))
            ?? throw new AgentToolException(Format(Strings.NoComposeFile, string.Join(", ", DockerCommands.ComposeFiles)));
    }

    // A relative host path ("./data:/data") is resolved from the workspace root: not every docker version handles it.
    private string Volume(string volume) =>
        volume.StartsWith('.') && volume.IndexOf(':', StringComparison.Ordinal) is > 0 and var separator
            ? WorkspacePaths.Resolve(workspace, volume[..separator]) + volume[separator..]
            : volume;

    private string? RelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = Path.GetRelativePath(docker.Root, WorkspacePaths.Resolve(workspace, path));
        return relative == "." ? null : relative.Replace('\\', '/');
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
