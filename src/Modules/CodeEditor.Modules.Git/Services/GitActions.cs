using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Services;

/// <summary>
/// Panel actions on the repository: index, discard, commit, branches, remote sync, init. Each goes through
/// <see cref="GitRepository.RunAsync"/>: one at a time, with progress and errors in the panel and status bar, followed by a
/// state refresh. Many paths take several commands (<see cref="GitArguments.Batches"/>).
/// </summary>
public sealed class GitActions(GitRepository repository, GitCli git, IFileSystem fileSystem)
{
    public Task<bool> StageAsync(IReadOnlyList<string> paths) =>
        Run(Strings.ActionStage, (root, token) => RunBatchesAsync(root, paths, GitArguments.Stage, token));

    /// <summary>
    /// Stages a whole group. Without conflicts "Changes" is staged with one command, which also covers files beyond the
    /// shown limit.
    /// </summary>
    public Task<bool> StageGroupAsync(GitChangeGroup group)
    {
        var status = repository.Status;
        if (group == GitChangeGroup.Changes && !status.HasConflicts)
        {
            return Run(Strings.ActionStage, (root, token) => git.RunAsync(root, GitArguments.StageEverything, token));
        }

        var paths = Paths(status.In(group));
        return Run(Strings.ActionStage, (root, token) => RunBatchesAsync(root, paths, GitArguments.Stage, token));
    }

    public Task<bool> UnstageAsync(IReadOnlyList<string> paths)
    {
        var initial = repository.Status.Head.IsInitial;
        return Run(Strings.ActionUnstage, (root, token) => RunBatchesAsync(root, paths, batch => GitArguments.Unstage(batch, initial), token));
    }

    /// <summary>Unstages everything; with conflicts, only the staged group, so resolved-conflict marks stay.</summary>
    public Task<bool> UnstageAllAsync()
    {
        var status = repository.Status;
        if (status.HasConflicts)
        {
            return UnstageAsync(Paths(status.In(GitChangeGroup.Staged)));
        }

        var arguments = status.Head.IsInitial ? GitArguments.UnstageEverythingInitial : GitArguments.UnstageEverything;
        return Run(Strings.ActionUnstage, (root, token) => git.RunAsync(root, arguments, token));
    }

    /// <summary>Discards working tree changes; untracked files go to the recycle bin.</summary>
    public Task<bool> DiscardAsync(IReadOnlyList<GitFileChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var tracked = Paths(changes.Where(change => !change.IsUntracked));
        var untracked = Paths(changes.Where(change => change.IsUntracked));
        return Run(Strings.ActionDiscard, async (root, token) =>
        {
            await RunBatchesAsync(root, tracked, GitArguments.Discard, token);
            foreach (var path in untracked)
            {
                fileSystem.DeleteToRecycleBin(Path.GetFullPath(Path.Combine(root, path)));
            }
        });
    }

    /// <param name="stageAll">Stage all changes first (the index is empty and the user agreed).</param>
    public Task<bool> CommitAsync(string message, bool stageAll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return Run(Strings.ActionCommit, async (root, token) =>
        {
            if (stageAll)
            {
                await git.RunAsync(root, GitArguments.StageEverything, token);
            }

            await git.RunAsync(root, GitArguments.Commit(message), token);
        });
    }

    /// <summary>Switches branches; for a remote branch, creates a local one that tracks it.</summary>
    public Task<bool> SwitchAsync(GitBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var arguments = branch.IsRemote ? GitArguments.Track(branch.Name) : GitArguments.Switch(branch.Name);
        return Run(Format(Strings.ActionSwitch, branch.Name), (root, token) => git.RunAsync(root, arguments, token));
    }

    public Task<bool> CreateBranchAsync(string name) =>
        Run(Format(Strings.ActionCreateBranch, name), (root, token) => git.RunAsync(root, GitArguments.CreateBranch(name), token));

    public Task<bool> PullAsync() =>
        Run(Strings.ActionPull, (root, token) => git.RunAsync(root, GitArguments.Pull, token, GitRunner.NetworkTimeout));

    /// <summary>Pushes the branch; a branch without an upstream is created in <c>origin</c>.</summary>
    public Task<bool> PushAsync()
    {
        var arguments = repository.Status.Head.Upstream is null ? GitArguments.PushNewBranch : GitArguments.Push;
        return Run(Strings.ActionPush, (root, token) => git.RunAsync(root, arguments, token, GitRunner.NetworkTimeout));
    }

    public Task<bool> FetchAsync() =>
        Run(Strings.ActionFetch, (root, token) => git.RunAsync(root, GitArguments.Fetch, token, GitRunner.NetworkTimeout));

    /// <summary>Runs <c>git init</c> in the workspace, which has no repository yet.</summary>
    public Task<bool> InitAsync() =>
        repository.RunAsync(Strings.ActionInit, (_, token) => git.RunAsync(root: null, GitArguments.Init, token));

    private Task<bool> Run(string name, Func<string, CancellationToken, Task> action) =>
        repository.RunAsync(name, (root, token) => root is null ? throw new GitException(Strings.NoRepository) : action(root, token));

    private async Task RunBatchesAsync(string root, IReadOnlyList<string> paths, Func<IReadOnlyList<string>, IReadOnlyList<string>> command, CancellationToken token)
    {
        foreach (var batch in GitArguments.Batches(paths))
        {
            await git.RunAsync(root, command(batch), token);
        }
    }

    // A file in two groups ("MM") yields one path.
    private static List<string> Paths(IEnumerable<GitFileChange> changes) =>
        [.. changes.Select(change => change.Path).Distinct(StringComparer.Ordinal)];

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
