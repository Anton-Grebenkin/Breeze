using System.Collections.Immutable;

namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A <c>git status</c> snapshot: the branch and changed files by panel group.</summary>
public sealed record GitStatus(GitHead Head, ImmutableArray<GitFileChange> Changes)
{
    public static GitStatus Empty { get; } = new(GitHead.Empty, []);

    public bool HasStaged => Changes.Any(change => change.Group == GitChangeGroup.Staged);

    /// <summary>There are unstaged changes that can be committed all at once.</summary>
    public bool HasUnstaged => Changes.Any(change => change.Group == GitChangeGroup.Changes);

    public bool HasConflicts => Changes.Any(change => change.Group == GitChangeGroup.Merge);

    public IEnumerable<GitFileChange> In(GitChangeGroup group) => Changes.Where(change => change.Group == group);
}
