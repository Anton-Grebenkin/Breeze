namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A changed file in a panel group. A file changed both in the index and after it is in both groups.</summary>
/// <param name="Path">Path relative to the repository root with <c>/</c>.</param>
/// <param name="OriginalPath">Previous path of a renamed or copied file.</param>
public sealed record GitFileChange(GitChangeGroup Group, GitFileStatus Status, string Path, string? OriginalPath = null)
{
    /// <summary>The file is deleted: only its changes can be opened.</summary>
    public bool IsDeleted => Status == GitFileStatus.Deleted;

    public bool IsUntracked => Status == GitFileStatus.Untracked;
}
