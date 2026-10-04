namespace CodeEditor.Modules.Git.Services.Agent;

/// <summary>A repository read requested via the <c>git</c> tool (<see cref="GitCommands.Read"/>).</summary>
/// <param name="Path">File or folder, root-relative with <c>/</c>; <c>null</c> means the whole repository.</param>
/// <param name="Revision">Commit, branch or range: what diff compares with, the log range, or the commit to show.</param>
public sealed record GitRead(string Action, string? Path = null, string? Revision = null, bool Staged = false, int MaxCount = GitCommands.DefaultLogCount)
{
    /// <summary>blame: first line of the range.</summary>
    public int? StartLine { get; init; }

    /// <summary>blame: last line of the range.</summary>
    public int? EndLine { get; init; }
}
