using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Services;

/// <summary>
/// Repository reads for the panel: location, status, branches, history, commit files and diffs, file diffs. git runs and
/// its output is parsed in the background; the UI thread gets a ready model.
/// </summary>
public sealed class GitReader(GitCli git)
{
    /// <summary>History page size; "Show More" loads the next one.</summary>
    public const int LogPageSize = 200;

    private const int NotRepositoryExitCode = 128;

    /// <returns>Root and .git folder; <c>null</c> when the workspace is not in a repository.</returns>
    /// <exception cref="GitException">git is not installed or could not read the repository.</exception>
    public async Task<GitLocation?> LocateAsync(CancellationToken cancellationToken)
    {
        GitOutput output;
        try
        {
            output = await git.ReadAsync(root: null, GitArguments.Locate, cancellationToken).ConfigureAwait(false);
        }
        catch (GitException exception) when (IsNotRepository(exception))
        {
            return null;
        }

        var lines = output.Lines.Where(line => line.Length > 0).ToList();
        return lines.Count >= 2 ? new GitLocation(Path.GetFullPath(lines[0]), Path.GetFullPath(lines[1])) : null;
    }

    /// <exception cref="GitException">git could not read the status.</exception>
    public async Task<GitStatus> StatusAsync(string root, CancellationToken cancellationToken)
    {
        var output = await git.ReadAsync(root, GitArguments.Status, cancellationToken).ConfigureAwait(false);

        // -z records are NUL-separated; lines without NUL are git warnings from stderr.
        return GitStatusParser.Parse(string.Join('\n', output.Lines.Where(line => line.Contains('\0', StringComparison.Ordinal))));
    }

    /// <exception cref="GitException">git could not read the branches.</exception>
    public async Task<IReadOnlyList<GitBranch>> BranchesAsync(string root, CancellationToken cancellationToken)
    {
        var output = await git.ReadAsync(root, GitArguments.Branches, cancellationToken).ConfigureAwait(false);
        return GitBranchParser.Parse(output.Lines);
    }

    /// <exception cref="GitException">git could not read the history.</exception>
    public async Task<IReadOnlyList<GitCommitInfo>> LogAsync(string root, int skip, CancellationToken cancellationToken)
    {
        var output = await git.ReadAsync(root, GitArguments.Log(skip, LogPageSize), cancellationToken).ConfigureAwait(false);
        return GitLogParser.ParseLog(output.Text);
    }

    /// <exception cref="GitException">git could not read the commit.</exception>
    public async Task<IReadOnlyList<GitCommitFile>> CommitFilesAsync(string root, string hash, CancellationToken cancellationToken)
    {
        var output = await git.ReadAsync(root, GitArguments.CommitFiles(hash), cancellationToken).ConfigureAwait(false);
        return GitLogParser.ParseFiles(output.Text);
    }

    /// <summary>Changes of a panel file: staged, in the working tree, or a whole untracked file.</summary>
    /// <exception cref="GitException">git could not build the diff.</exception>
    public async Task<IReadOnlyList<GitDiffRow>> DiffAsync(string root, GitFileChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var output = await git.ReadAsync(root, GitArguments.Diff(change), cancellationToken, differencesAreSuccess: change.IsUntracked).ConfigureAwait(false);
        return Rows(output);
    }

    /// <exception cref="GitException">git could not build the diff.</exception>
    public async Task<IReadOnlyList<GitDiffRow>> CommitDiffAsync(string root, string hash, GitCommitFile file, CancellationToken cancellationToken)
    {
        var output = await git.ReadAsync(root, GitArguments.CommitDiff(hash, file), cancellationToken).ConfigureAwait(false);
        return Rows(output);
    }

    // A single file's header repeats the path shown above the diff, so it is dropped.
    private static IReadOnlyList<GitDiffRow> Rows(GitOutput output)
    {
        var rows = GitDiffParser.Parse(output.Lines);
        var single = rows.Count > 0 && rows[0].Kind == GitDiffRowKind.File && rows.Count(row => row.Kind == GitDiffRowKind.File) == 1;
        IEnumerable<GitDiffRow> shown = single ? rows.Skip(1) : rows;
        if (!output.Truncated)
        {
            return single ? [.. shown] : rows;
        }

        var note = string.Format(CultureInfo.CurrentCulture, Strings.DiffTruncated, GitCli.MaxLines);
        return [.. shown, new GitDiffRow(GitDiffRowKind.Note, note)];
    }

    // "fatal: not a git repository (or any of the parent directories): .git"; localized git also ends with ": .git".
    private static bool IsNotRepository(GitException exception) =>
        exception.ExitCode == NotRepositoryExitCode
        && (exception.Message.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
            || exception.Message.TrimEnd().EndsWith(": .git", StringComparison.Ordinal));
}
