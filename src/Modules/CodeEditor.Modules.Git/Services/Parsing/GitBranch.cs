namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A branch to pick: local or remote (<c>origin/feature</c>), with its commit and subject.</summary>
/// <param name="Name">Short name: <c>main</c>, <c>origin/feature</c>.</param>
public sealed record GitBranch(string Name, bool IsRemote, string Commit, string Subject)
{
    public bool IsCurrent { get; init; }

    public string? Upstream { get; init; }

    /// <summary>Local branch created when switching to a remote one: <c>origin/feature</c> → <c>feature</c>.</summary>
    public string LocalName => IsRemote && Name.IndexOf('/', StringComparison.Ordinal) is var slash and >= 0 ? Name[(slash + 1)..] : Name;
}
