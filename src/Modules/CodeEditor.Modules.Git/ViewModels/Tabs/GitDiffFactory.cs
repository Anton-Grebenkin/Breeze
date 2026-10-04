using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// Creates diffs for tabs: panel file changes (which follow the repository) and history commit files (immutable).
/// "Open File" is available only when the file exists on disk.
/// </summary>
public sealed class GitDiffFactory(GitRepository repository, GitReader reader, IFileSystem fileSystem, ICommandService commands, ISystemShell shell)
{
    public GitDiffViewModel ForChange(GitFileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var root = repository.Location?.Root ?? string.Empty;
        var source = new GitDiffSource(Title(change), change.Path, ExistingFile(change.Path), token => reader.DiffAsync(root, change, token));
        return new GitDiffViewModel(source, commands, shell, repository);
    }

    public GitDiffViewModel ForCommitFile(string hash, GitCommitFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var root = repository.Location?.Root ?? string.Empty;
        var source = new GitDiffSource(GitStatusLabels.FileName(file.Path), file.Path, ExistingFile(file.Path), token => reader.CommitDiffAsync(root, hash, file, token));
        return new GitDiffViewModel(source, commands, shell);
    }

    /// <summary>Tab title: "Order.cs (changes)" or "Order.cs (index)".</summary>
    public static string Title(GitFileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var format = change.Group == GitChangeGroup.Staged ? Strings.DiffTitleStaged : Strings.DiffTitleChanges;
        return string.Format(CultureInfo.CurrentCulture, format, GitStatusLabels.FileName(change.Path));
    }

    private string? ExistingFile(string path) => repository.FullPath(path) is { } full && fileSystem.FileExists(full) ? full : null;
}
