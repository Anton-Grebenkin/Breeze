using CodeEditor.Modules.Git.Services.Parsing;

namespace CodeEditor.Modules.Git.Services;

/// <summary>Git panel context keys for the <c>when</c> conditions of commands, key bindings and menu items.</summary>
public static class GitContextKeys
{
    /// <summary>
    /// Repository state: <see cref="Ready"/>, <see cref="NotRepository"/>, <c>noGit</c>, <c>noFolder</c>, <c>unknown</c>.
    /// </summary>
    public const string State = "gitState";

    public const string Ready = "ready";

    public const string NotRepository = "notRepository";

    /// <summary>Group of the selected change list row: <c>merge</c>, <c>staged</c> or <c>changes</c>.</summary>
    public const string ResourceGroup = "gitResourceGroup";

    /// <summary>A group header is selected, not a file.</summary>
    public const string ResourceIsGroup = "gitResourceIsGroup";

    /// <summary>The selected file is missing from the working tree; only its changes can be opened.</summary>
    public const string ResourceDeleted = "gitResourceDeleted";

    /// <summary>Focus is in the change list (<c>Enter</c> opens changes).</summary>
    public const string ChangesFocus = "gitChangesFocus";

    /// <summary>Focus is in the commit message box (<c>Ctrl+Enter</c> commits).</summary>
    public const string CommitInputFocus = "gitCommitInputFocus";

    public static string StateValue(GitRepositoryState state) => state switch
    {
        GitRepositoryState.NoFolder => "noFolder",
        GitRepositoryState.NoGit => "noGit",
        GitRepositoryState.NotRepository => NotRepository,
        GitRepositoryState.Ready => Ready,
        _ => "unknown",
    };

    public static string GroupValue(GitChangeGroup group) => group switch
    {
        GitChangeGroup.Merge => "merge",
        GitChangeGroup.Staged => "staged",
        _ => "changes",
    };
}
