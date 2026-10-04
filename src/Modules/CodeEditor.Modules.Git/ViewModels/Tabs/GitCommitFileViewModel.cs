using CodeEditor.Modules.Git.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// A commit file in the history: status letter, name and folder. Immutable, like <see cref="GitLogEntryViewModel"/>.
/// </summary>
public sealed class GitCommitFileViewModel(GitCommitFile file) : ObservableObject
{
    public GitCommitFile File { get; } = file;

    public string Path => File.Path;

    public string Name => GitStatusLabels.FileName(File.Path);

    public string Directory => GitStatusLabels.Directory(File.Path);

    public GitFileStatus Status => File.Status;

    public string Letter => GitStatusLabels.Letter(File.Status);

    public bool IsDeleted => File.Status == GitFileStatus.Deleted;

    public string ToolTip => File.OriginalPath is { } original
        ? $"{File.Path} ← {original}"
        : File.Path;

    public string AutomationId => $"Git.CommitFile.{File.Path}";

    public override string ToString() => File.Path;
}
