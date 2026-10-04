namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>Current branch from the <c>git status --branch</c> headers.</summary>
/// <param name="Branch">Branch name; <c>null</c> when HEAD is detached (a commit or tag is checked out).</param>
/// <param name="Commit">HEAD commit; <c>null</c> when there are no commits yet.</param>
public sealed record GitHead(string? Branch, string? Commit)
{
    private const int ShortHashLength = 7;

    public static GitHead Empty { get; } = new(null, null);

    /// <summary>Upstream branch, e.g. <c>origin/main</c>; <c>null</c> when there is none.</summary>
    public string? Upstream { get; init; }

    /// <summary>Commits missing from the upstream (sent by push).</summary>
    public int Ahead { get; init; }

    /// <summary>Upstream commits missing locally (received by pull).</summary>
    public int Behind { get; init; }

    public bool IsDetached => Branch is null && Commit is not null;

    /// <summary>No commits yet: unstaging only works via <c>git rm --cached</c>.</summary>
    public bool IsInitial => Commit is null;

    /// <summary>Short hash of a detached HEAD.</summary>
    public string? ShortCommit => Commit is { Length: > ShortHashLength } commit ? commit[..ShortHashLength] : Commit;
}
