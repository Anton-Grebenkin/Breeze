namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>Kind of diff row.</summary>
public enum GitDiffRowKind
{
    File,
    Hunk,
    Context,
    Added,
    Removed,

    /// <summary>A note: new or deleted file, rename, binary file, no newline at end of file.</summary>
    Note,
}
