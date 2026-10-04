namespace CodeEditor.Modules.Git.Services;

/// <summary>What the panel knows about the workspace repository.</summary>
public enum GitRepositoryState
{
    /// <summary>Not read yet: the folder has just been opened.</summary>
    Unknown,

    NoFolder,

    /// <summary>git is not installed or not on PATH.</summary>
    NoGit,

    /// <summary>The folder is not in a repository; the panel offers <c>git init</c>.</summary>
    NotRepository,

    Ready,
}
