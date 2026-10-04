using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Output;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Agent tool <c>run_command</c>: a Windows PowerShell 5.1 command in the workspace (<see cref="PowerShellLauncher"/>:
/// UTF-8, correct exit code, <c>&amp;&amp;</c>) unless <see cref="CommandPolicy"/> forbids it. Read-only and
/// user-allowed commands run without asking (<see cref="CommandApprovals"/>); others need approval (the card shows the
/// command and folder). Input is closed; output goes to the Commands channel and to the model, long output as head and
/// tail with a file link (<see cref="IAgentOutputStore"/>).
/// The command is awaited for at most <c>waitSeconds</c> (ADR 0026, as in Codex); if still running it continues in
/// the background (<see cref="BackgroundCommands"/>) and the model gets its id and output so far. No need to choose
/// up front: long tests don't die on a timeout and servers don't block the turn.
/// </summary>
public sealed class CommandAgentTools(
    IWorkspace workspace,
    IFileSystem fileSystem,
    IOutputService output,
    SaveBeforeRun saveBeforeRun,
    IAgentOutputStore outputs,
    BackgroundCommands background) : IAgentToolProvider, IAgentChangePreviewer
{
    public const string RunCommandName = "run_command";
    public static string ChannelName => Strings.Commands;

    /// <summary>Default wait for the command to finish before it moves to the background.</summary>
    public const int DefaultWaitSeconds = 30;

    public const int MaxWaitSeconds = 600;

    public IEnumerable<AITool> CreateTools() =>
    [
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RunCommandAsync, RunCommandName,
            "Runs a Windows PowerShell 5.1 command in the workspace (or the cwd folder). Waits up to waitSeconds (default 30) and returns the exit code and output; " +
            "a command still running then keeps running in the background: you get its id and the output so far — wait for more with command_output (it can wait for a line matching a regex), stop it with stop_command. " +
            "Use waitSeconds about 5 to start servers and watchers, up to 600 for long one-off jobs. " +
            "Read-only commands (listing files, dotnet --info) run without asking; others need the user's approval, and the user can allow a command prefix permanently. " +
            "Use cwd instead of cd; && and || work. Prefer build and run_tests for building and testing, git for the repository, read_file and search_text for reading and searching files. " +
            "No interactive input: the command gets end of input. Long output is saved to a file: you get its start and end and the path.")),
    ];

    public bool CanPreview(string toolName) => toolName == RunCommandName;

    public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var command = ToolArguments.Get<string>(arguments, "command");
        CommandPolicy.EnsureAllowed(command);
        var folder = Folder(arguments.TryGetValue("cwd", out var cwd) ? cwd?.ToString() : null);
        return Task.FromResult<IReadOnlyList<FileChangePreview>>([new FileChangePreview(ProposedChangeKind.Command, workspace.RelativePath(folder), string.Empty, command)]);
    }

    private async Task<string> RunCommandAsync(
        [Description("PowerShell command line.")] string command,
        [Description("Folder relative to the workspace root; default — the root. Use it instead of cd.")] string? cwd = null,
        [Description("How long to wait for the command to finish, 0–600 seconds; default 30. If it is still running then, it keeps running in the background.")] int waitSeconds = DefaultWaitSeconds,
        CancellationToken cancellationToken = default)
    {
        CommandPolicy.EnsureAllowed(command);
        var folder = Folder(cwd);
        await saveBeforeRun.SaveAsync(cancellationToken);
        var channel = output.GetOrCreate(ChannelName);
        channel.AppendLine($"> {command}");
        var started = background.Start(PowerShellLauncher.Request(command, folder, Timeout.InfiniteTimeSpan), command, channel.AppendLine);
        try
        {
            await started.WaitAsync(pattern: null, TimeSpan.FromSeconds(Math.Clamp(waitSeconds, 0, MaxWaitSeconds)), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The turn was cancelled: stop this step's command with it.
            background.Stop(started.Id);
            throw;
        }

        if (started.IsRunning)
        {
            return outputs.Fit(background.Report(started, started.ReadNew()), RunCommandName);
        }

        background.Forget(started.Id);
        return outputs.Fit(Report(started, background.Elapsed(started)), RunCommandName);
    }

    // The first line is the summary (parsed by the feed row), then a hint for a non-obvious exit code, then output.
    private static string Report(BackgroundCommand command, TimeSpan elapsed)
    {
        var output = command.ReadNew().TrimEnd();
        if (command.ExitCode is not { } exitCode)
        {
            // The process didn't start; the output says why.
            throw new AgentToolException(output);
        }

        var header = Format(Strings.CommandExitCode, exitCode, Durations.Seconds(elapsed));
        if (CommandExitHints.For(command.Command, exitCode) is { } hint)
        {
            header += "\n" + hint;
        }

        return output.Length == 0 ? header + Strings.NoOutput : header + "\n" + output;
    }

    private string Folder(string? cwd)
    {
        var folder = WorkspacePaths.Resolve(workspace, cwd);
        return fileSystem.DirectoryExists(folder) ? folder : throw new AgentToolException(Format(Strings.FolderNotFound, cwd ?? string.Empty));
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
