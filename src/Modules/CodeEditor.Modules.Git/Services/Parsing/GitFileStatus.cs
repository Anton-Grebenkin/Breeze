namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>What happened to a file; the panel shows M, A, D, R, C, T, U, or '!' for a conflict, as VS Code does.</summary>
public enum GitFileStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Untracked,
    Conflict,
}
