namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>Panel change group, in display order: merge conflicts, index, working tree.</summary>
public enum GitChangeGroup
{
    /// <summary>Files with merge conflicts; staging marks a conflict resolved.</summary>
    Merge,

    /// <summary>Staged changes: what the commit will contain.</summary>
    Staged,

    /// <summary>Unstaged working tree changes and untracked files.</summary>
    Changes,
}
