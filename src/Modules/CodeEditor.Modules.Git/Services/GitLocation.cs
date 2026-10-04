namespace CodeEditor.Modules.Git.Services;

/// <summary>Repository of the workspace: its root and .git folder (a linked worktree has its own).</summary>
public sealed record GitLocation(string Root, string GitDirectory);
