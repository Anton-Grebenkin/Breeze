namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>A repository change requested via <c>git_change</c> (<see cref="GitCommands.Change"/>).</summary>
/// <param name="Message">Commit message or stash description.</param>
/// <param name="Files">Files to commit, root-relative with <c>/</c>; empty means all changes of tracked files.</param>
/// <param name="Path">What to restore, relative to the root.</param>
public sealed record GitChange(string Action, string? Message = null, IReadOnlyList<string>? Files = null, string? Branch = null, string? Path = null);
