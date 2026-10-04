using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Git.Services.Cli;

/// <summary>
/// Runs git in the workspace root without a shell: arguments are passed as a list with no quoting or escaping, so commit
/// messages with newlines and quotes arrive intact. No pager or color, non-ASCII names are not escaped, and there is no
/// terminal password prompt, so a command that needs input exits at once. Secret variables are removed from the
/// environment because the output goes to the model.
/// </summary>
public sealed class GitRunner(IProcessRunner runner, IWorkspace workspace)
{
    public const string Executable = "git";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Push goes over the network and may wait for sign-in.</summary>
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromMinutes(3);

    private static readonly string[] Options = ["--no-pager", "-c", "core.quotepath=false", "-c", "color.ui=false"];

    private static readonly Dictionary<string, string> Environment = new(StringComparer.Ordinal) { ["GIT_TERMINAL_PROMPT"] = "0" };

    /// <summary>Workspace root.</summary>
    /// <exception cref="AgentToolException">No folder is open.</exception>
    public string Root => WorkspacePaths.Resolve(workspace, relativePath: null);

    /// <exception cref="AgentToolException">No folder is open.</exception>
    /// <exception cref="InvalidOperationException">git is not installed.</exception>
    public Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        RunAsync(arguments, onLine: null, cancellationToken, timeout);

    /// <param name="onLine">
    /// Receives each output line (stdout and stderr) as it arrives, on background threads. The panel uses it to get the
    /// full output: <see cref="ProcessResult.Output"/> drops the middle of long output.
    /// </param>
    /// <exception cref="AgentToolException">No folder is open.</exception>
    /// <exception cref="InvalidOperationException">git is not installed.</exception>
    public Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, Action<string>? onLine, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var request = new ProcessRequest(Executable, [.. Options, .. arguments], Root)
        {
            Timeout = timeout ?? Timeout,
            Environment = Environment,
            HideSecretVariables = true,
        };
        return runner.RunAsync(request, onLine, cancellationToken);
    }
}
