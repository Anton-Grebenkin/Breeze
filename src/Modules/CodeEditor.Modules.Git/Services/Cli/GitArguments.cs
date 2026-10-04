using System.Buffers;
using System.Globalization;
using CodeEditor.Modules.Git.Services.Agent;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Services.Cli;

/// <summary>
/// git arguments for the panel. Paths are relative to the repository root and always follow "--"; branch names and hashes
/// may not start with '-', so they never become git options. Diffs force "a/" and "b/" prefixes over user settings and
/// disable external diff tools.
/// </summary>
public static class GitArguments
{
    /// <summary>Path characters per command: the Windows command line is limited to 32,767 characters.</summary>
    public const int MaxPathCharacters = 24_000;

    private static readonly string[] DiffOptions = ["--no-ext-diff", "--src-prefix=a/", "--dst-prefix=b/"];

    // Characters forbidden in branch names (git check-ref-format), plus whitespace.
    private static readonly SearchValues<char> ForbiddenInBranch = SearchValues.Create(" ~^:?*[\\\t\r\n");

    /// <summary>Repository root and .git folder for the workspace; fails when the folder is not in a repository.</summary>
    public static IReadOnlyList<string> Locate { get; } = ["rev-parse", "--show-toplevel", "--absolute-git-dir"];

    /// <summary>NUL-separated status with the branch and every untracked file (not just its folder).</summary>
    public static IReadOnlyList<string> Status { get; } = ["status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all"];

    /// <summary>Local and remote branches, most recent first.</summary>
    public static IReadOnlyList<string> Branches { get; } =
        ["for-each-ref", "--sort=-committerdate", GitBranchParser.Format, "refs/heads", "refs/remotes"];

    public static IReadOnlyList<string> Init { get; } = ["init"];

    public static IReadOnlyList<string> Pull { get; } = ["pull"];

    public static IReadOnlyList<string> Push { get; } = ["push"];

    public static IReadOnlyList<string> Fetch { get; } = ["fetch"];

    /// <summary>Stages all working tree changes, including new and deleted files.</summary>
    public static IReadOnlyList<string> StageEverything { get; } = ["add", "-A"];

    /// <summary>Resets the index to HEAD; the working tree is untouched.</summary>
    public static IReadOnlyList<string> UnstageEverything { get; } = ["reset", "-q"];

    /// <summary>Unstages everything before the first commit, when there is no HEAD and <c>git reset</c> fails.</summary>
    public static IReadOnlyList<string> UnstageEverythingInitial { get; } = ["rm", "--cached", "-r", "-q", "--", "."];

    public static IReadOnlyList<string> Stage(IReadOnlyList<string> paths) => ["add", "-A", "--", .. paths];

    public static IReadOnlyList<string> Unstage(IReadOnlyList<string> paths, bool initial) =>
        initial ? ["rm", "--cached", "-r", "-q", "--", .. paths] : ["restore", "--staged", "--", .. paths];

    /// <summary>Restores the working tree from the index; edits made after staging are lost.</summary>
    public static IReadOnlyList<string> Discard(IReadOnlyList<string> paths) => ["restore", "--worktree", "--", .. paths];

    public static IReadOnlyList<string> Commit(string message) => ["commit", "-m", message];

    public static IReadOnlyList<string> Switch(string branch) => ["switch", Name(branch)];

    /// <summary>Switches to a remote branch by creating a local one with the same name that tracks it.</summary>
    public static IReadOnlyList<string> Track(string remoteBranch) => ["switch", "--track", Name(remoteBranch)];

    public static IReadOnlyList<string> CreateBranch(string name) => ["switch", "-c", Name(name)];

    /// <summary>Pushes a branch that has no upstream, creating it in <c>origin</c>.</summary>
    public static IReadOnlyList<string> PushNewBranch => GitCommands.PushWithUpstream;

    /// <summary>Diff of a panel file: index vs HEAD, working tree vs index, or an untracked file vs empty.</summary>
    public static IReadOnlyList<string> Diff(GitFileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return change switch
        {
            { IsUntracked: true } => ["diff", "--no-index", .. DiffOptions, "--", "/dev/null", change.Path],
            { Group: GitChangeGroup.Staged, OriginalPath: { } original } => ["diff", "--cached", "-M", .. DiffOptions, "--", original, change.Path],
            { Group: GitChangeGroup.Staged } => ["diff", "--cached", .. DiffOptions, "--", change.Path],
            _ => ["diff", .. DiffOptions, "--", change.Path],
        };
    }

    /// <summary>One page of HEAD history.</summary>
    public static IReadOnlyList<string> Log(int skip, int count) =>
        ["log", "-z", "--no-show-signature", GitLogParser.Format, Invariant($"--skip={skip}"), "-n", Invariant($"{count}")];

    /// <summary>Files of a commit; for a merge, changes against the first parent.</summary>
    public static IReadOnlyList<string> CommitFiles(string hash) =>
        ["show", "--format=", "--name-status", "-z", "--diff-merges=first-parent", Name(hash)];

    public static IReadOnlyList<string> CommitDiff(string hash, GitCommitFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        IReadOnlyList<string> paths = file.OriginalPath is { } original ? [original, file.Path] : [file.Path];
        return ["show", "--format=", "-M", "--diff-merges=first-parent", .. DiffOptions, Name(hash), "--", .. paths];
    }

    /// <summary>Splits paths into batches of at most <see cref="MaxPathCharacters"/> characters.</summary>
    public static IEnumerable<IReadOnlyList<string>> Batches(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var batch = new List<string>();
        var length = 0;
        foreach (var path in paths)
        {
            if (batch.Count > 0 && length + path.Length > MaxPathCharacters)
            {
                yield return batch;
                (batch, length) = ([], 0);
            }

            batch.Add(path);
            length += path.Length + 1;
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    /// <summary>
    /// Simplified git check-ref-format for a new branch: no whitespace or "~^:?*[\", no leading '-', '.' or '/', no "..",
    /// "@{", "//" or "/.", and no trailing '/', '.' or ".lock".
    /// </summary>
    public static bool IsValidBranchName(string? name) =>
        !string.IsNullOrEmpty(name)
        && name != "@"
        && name[0] is not ('-' or '.' or '/')
        && name[^1] is not ('/' or '.')
        && !name.EndsWith(".lock", StringComparison.Ordinal)
        && !name.AsSpan().ContainsAny(ForbiddenInBranch)
        && !name.Any(char.IsControl)
        && !ContainsAny(name, "..", "@{", "//", "/.");

    private static bool ContainsAny(string text, params string[] parts) =>
        parts.Any(part => text.Contains(part, StringComparison.Ordinal));

    private static string Name(string value) =>
        value.Length == 0 || value.StartsWith('-') ? throw new ArgumentException($"Invalid git name '{value}'.", nameof(value)) : value;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
