namespace CodeEditor.Modules.Git.Commands;

/// <summary>
/// Git panel command ids. File actions take a change (<c>GitFileChange</c>) or a panel row as the argument; without one
/// they use the selected row, otherwise the active editor's file, as in VS Code.
/// </summary>
public static class GitCommandIds
{
    public const string Commit = "git.commit";
    public const string Stage = "git.stage";
    public const string Unstage = "git.unstage";

    /// <summary>Argument: a <c>GitChangeGroup</c>; without one, the selected group or "Changes".</summary>
    public const string StageAll = "git.stageAll";
    public const string UnstageAll = "git.unstageAll";
    public const string Discard = "git.discard";
    public const string Refresh = "git.refresh";
    public const string Checkout = "git.checkout";
    public const string CreateBranch = "git.createBranch";
    public const string Pull = "git.pull";
    public const string Push = "git.push";
    public const string Fetch = "git.fetch";
    public const string ShowHistory = "git.showHistory";
    public const string OpenChanges = "git.openChanges";
    public const string OpenFile = "git.openFile";
    public const string Init = "git.init";
}
