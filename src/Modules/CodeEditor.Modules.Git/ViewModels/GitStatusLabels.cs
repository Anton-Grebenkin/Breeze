using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>File status letter and name, as in VS Code: M, A, D, R, C, T, U, and '!' for a conflict.</summary>
public static class GitStatusLabels
{
    public static string Letter(GitFileStatus status) => status switch
    {
        GitFileStatus.Added => "A",
        GitFileStatus.Deleted => "D",
        GitFileStatus.Renamed => "R",
        GitFileStatus.Copied => "C",
        GitFileStatus.TypeChanged => "T",
        GitFileStatus.Untracked => "U",
        GitFileStatus.Conflict => "!",
        _ => "M",
    };

    public static string Describe(GitFileStatus status) => status switch
    {
        GitFileStatus.Added => Strings.StatusAdded,
        GitFileStatus.Deleted => Strings.StatusDeleted,
        GitFileStatus.Renamed => Strings.StatusRenamed,
        GitFileStatus.Copied => Strings.StatusCopied,
        GitFileStatus.TypeChanged => Strings.StatusTypeChanged,
        GitFileStatus.Untracked => Strings.StatusUntracked,
        GitFileStatus.Conflict => Strings.StatusConflict,
        _ => Strings.StatusModified,
    };

    /// <summary>File name from a '/'-separated path.</summary>
    public static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Folder from a '/'-separated path; empty for a file in the root.</summary>
    public static string Directory(string path) => path.LastIndexOf('/') is var slash and > 0 ? path[..slash] : string.Empty;
}
