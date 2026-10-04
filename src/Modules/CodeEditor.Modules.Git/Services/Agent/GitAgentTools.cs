using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Cli;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>
/// Agent tools for the workspace repository. <c>git</c> only reads (status, diff, log, show, blame) and runs without
/// asking in every mode (<see cref="ReadOnlyAIFunction"/>). <c>git_change</c> changes the repository; every call goes
/// through a card listing the git commands (<see cref="IAgentChangePreviewer"/>). Push only on the user's request and
/// always with a question (<see cref="GitApprovals"/>). git runs without a shell (<see cref="GitRunner"/>), so commit
/// messages with quotes and newlines survive intact, unlike in PowerShell.
/// </summary>
public sealed class GitAgentTools(GitRunner git, IWorkspace workspace, IAgentOutputStore outputs) : IAgentToolProvider, IAgentChangePreviewer
{
    public const string ReadName = "git";
    public const string ChangeName = "git_change";

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(ReadAsync, ReadName,
            "Reads the git repository of the workspace without changing it: status (branch and changed files), diff (changes with a summary per file), " +
            "log (commit history), show (one commit with its changes) or blame (who last changed each line). Use it instead of run_command for git. " +
            "Long output is saved to a file: you get its start and end and the path.")),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ChangeAsync, ChangeName,
            "Changes the git repository of the workspace; the user sees and approves each call. Actions: commit (stage files and commit), " +
            "create_branch (create a branch and switch to it), switch (switch to an existing branch), stash and stash_pop (put uncommitted changes aside and bring them back), " +
            "restore (discard uncommitted changes in a path; cannot be undone), push (send the current branch to the remote). " +
            "Push only when the user explicitly asked to push in this conversation. Check the status before committing and write the message in the style of the repository history.")),
    ];

    public bool CanPreview(string toolName) => toolName == ChangeName;

    public async Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var change = new GitChange(
            ToolArguments.Get<string>(arguments, "action"),
            ToolArguments.GetOptional<string>(arguments, "message"),
            ToolArguments.GetOptional<string[]>(arguments, "files"),
            ToolArguments.GetOptional<string>(arguments, "branch"),
            ToolArguments.GetOptional<string>(arguments, "path"));
        var commands = await CommandsAsync(Normalize(change), cancellationToken);
        return [new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, string.Join('\n', commands.Select(GitCommands.Display)))
        {
            Title = Strings.ApprovalTitle,
            Header = Strings.ApprovalHeader,
        }];
    }

    private async Task<string> ReadAsync(
        [Description("status, diff, log, show or blame.")] string action,
        [Description("File or folder relative to the workspace root: limits diff, log and show; required for blame.")] string? path = null,
        [Description("diff: compare with this commit or branch (main, HEAD~1); log: a range such as main..HEAD; show: the commit (default HEAD).")] string? revision = null,
        [Description("diff: staged changes instead of the working tree.")] bool staged = false,
        [Description("log: number of commits, up to 100; default 20.")] int maxCount = GitCommands.DefaultLogCount,
        [Description("blame: first line of the range.")] int? startLine = null,
        [Description("blame: last line of the range.")] int? endLine = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GitRead(action, RelativePath(path), NullIfEmpty(revision), staged, maxCount) { StartLine = startLine, EndLine = endLine };
        var output = await RunAsync(action, GitCommands.Read(request), GitRunner.Timeout, cancellationToken);
        return output.Length == 0 ? (action == GitCommands.Diff ? Strings.NoChanges : Strings.EmptyOutput) : outputs.Fit(output, ReadName);
    }

    private async Task<string> ChangeAsync(
        [Description("commit, create_branch, switch, stash, stash_pop, restore or push.")] string action,
        [Description("commit: the commit message (required); stash: an optional description.")] string? message = null,
        [Description("commit: files to commit, relative to the workspace root; empty — all changes of tracked files (new files must be listed).")] string[]? files = null,
        [Description("create_branch, switch: the branch name.")] string? branch = null,
        [Description("restore: the file or folder whose uncommitted changes are discarded.")] string? path = null,
        CancellationToken cancellationToken = default)
    {
        var change = Normalize(new GitChange(action, message, files, branch, path));
        var timeout = change.Action == GitCommands.Push ? GitRunner.NetworkTimeout : GitRunner.Timeout;
        var results = new List<string>();
        foreach (var command in await CommandsAsync(change, cancellationToken))
        {
            results.Add(await RunAsync(action, command, timeout, cancellationToken));
        }

        var text = string.Join('\n', results.Where(result => result.Length > 0));
        return text.Length == 0 ? Strings.Done : outputs.Fit(text, ChangeName);
    }

    // A branch without an upstream is pushed with --set-upstream: plain "git push" fails for it.
    private async Task<IReadOnlyList<IReadOnlyList<string>>> CommandsAsync(GitChange change, CancellationToken cancellationToken)
    {
        var commands = GitCommands.Change(change);
        if (change.Action != GitCommands.Push)
        {
            return commands;
        }

        var upstream = await git.RunAsync(GitCommands.Upstream, cancellationToken);
        return upstream.ExitCode == 0 ? commands : [GitCommands.PushWithUpstream];
    }

    private GitChange Normalize(GitChange change) => change with
    {
        Files = change.Files?.Select(RelativePath).OfType<string>().ToList(),
        Path = RelativePath(change.Path),
    };

    /// <exception cref="AgentToolException">git failed, timed out or is not installed.</exception>
    private async Task<string> RunAsync(string action, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await git.RunAsync(arguments, cancellationToken, timeout);
        }
        catch (InvalidOperationException exception)
        {
            throw new AgentToolException(exception.Message);
        }

        if (result.TimedOut)
        {
            throw new AgentToolException(Format(Strings.GitTimedOut, action, (int)timeout.TotalSeconds));
        }

        var output = result.Output.TrimEnd();
        return result.ExitCode == 0 ? output : throw new AgentToolException(Format(Strings.GitFailed, action, result.ExitCode, output));
    }

    // The model's path must be inside the workspace; git gets it relative to the root with '/'. The root means "all".
    private string? RelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = Path.GetRelativePath(git.Root, WorkspacePaths.Resolve(workspace, path));
        return relative == "." ? null : relative.Replace('\\', '/');
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
