using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Docker.Services.Cli;

/// <summary>
/// Runs docker without a shell: arguments as a list with no quoting or escaping. No "What's next" hints, color or
/// progress animation, since the model and the panel read the output. For the agent: in the workspace root without
/// secret variables, like other agent commands (compose takes variables from the project's <c>.env</c>). For the
/// panel: the user's environment (<see cref="RunForPanelAsync"/>).
/// </summary>
/// <param name="path">Folders to search for docker; <c>null</c> means PATH.</param>
/// <param name="extensions">Executable extensions; <c>null</c> means PATHEXT.</param>
public sealed class DockerRunner(IProcessRunner runner, IWorkspace workspace, IFileSystem fileSystem, string? path = null, string? extensions = null)
{
    public const string Executable = "docker";

    /// <summary>Reads and quick changes: state, logs, stop, remove.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>Build, image pull, a command in a container.</summary>
    public static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(15);

    private static readonly Dictionary<string, string> Environment = new(StringComparer.Ordinal)
    {
        ["DOCKER_CLI_HINTS"] = "false",
        ["NO_COLOR"] = "1",
        ["BUILDKIT_PROGRESS"] = "plain",
        ["COMPOSE_ANSI"] = "never",
        ["COMPOSE_PROGRESS"] = "plain",
    };

    /// <summary>docker is on PATH. Without it there are no tools, so the model wastes no tokens or steps on them.</summary>
    public bool IsInstalled => ExecutablePaths.Find(fileSystem, Executable, path, extensions).Any();

    /// <summary>Workspace root.</summary>
    /// <exception cref="AgentToolException">No folder is open.</exception>
    public string Root => WorkspacePaths.Resolve(workspace, relativePath: null);

    /// <exception cref="AgentToolException">No folder is open.</exception>
    /// <exception cref="InvalidOperationException">docker is not installed.</exception>
    public Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return runner.RunAsync(Request(arguments, Root, timeout, hideSecrets: true), onLine: null, cancellationToken);
    }

    /// <summary>
    /// A user action on the panel (ADR 0033). The environment matches the user's terminal: the user sees the output and
    /// compose substitutes the user's variables. Runs in the workspace root (compose files are relative to it), or in
    /// the user profile without a folder, since containers and images do not depend on it.
    /// </summary>
    /// <param name="onLine">Receives output lines as they arrive (streamed container logs), on background threads.</param>
    /// <exception cref="InvalidOperationException">docker is not installed.</exception>
    public Task<ProcessResult> RunForPanelAsync(IReadOnlyList<string> arguments, TimeSpan timeout, Action<string>? onLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var folder = workspace.Root ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        return runner.RunAsync(Request(arguments, folder, timeout, hideSecrets: false), onLine, cancellationToken);
    }

    private static ProcessRequest Request(IReadOnlyList<string> arguments, string folder, TimeSpan timeout, bool hideSecrets) =>
        new(Executable, arguments, folder)
        {
            Timeout = timeout,
            Environment = Environment,
            HideSecretVariables = hideSecrets,
        };
}
