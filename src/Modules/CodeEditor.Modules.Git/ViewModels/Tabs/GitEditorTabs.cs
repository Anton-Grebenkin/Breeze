using System.Globalization;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// git tabs in the editor area (ADR 0031): file changes are "git.diff:path", staged changes "git.diff.staged:path",
/// history "git.history". Opening again activates the existing tab.
/// </summary>
public sealed class GitEditorTabs(IEditorViews views, GitDiffFactory diffs, GitRepository repository, GitReader reader, ISystemShell shell)
{
    public const string DiffPrefix = "git.diff:";
    public const string StagedDiffPrefix = "git.diff.staged:";
    public const string HistoryId = "git.history";

    /// <summary>Tab id for a file's changes; staged and working tree changes get separate tabs.</summary>
    public static string DiffId(GitFileChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return (change.Group == GitChangeGroup.Staged ? StagedDiffPrefix : DiffPrefix) + change.Path;
    }

    /// <param name="preview">Open as a preview tab (single click in the panel, as in VS Code).</param>
    public void OpenChanges(GitFileChange change, bool preview)
    {
        ArgumentNullException.ThrowIfNull(change);
        var toolTip = change.Group == GitChangeGroup.Staged ? Strings.DiffToolTipStaged : Strings.DiffToolTipChanges;
        views.Open(new EditorViewRequest(DiffId(change), GitDiffFactory.Title(change), () => diffs.ForChange(change))
        {
            ToolTip = string.Format(CultureInfo.CurrentCulture, toolTip, change.Path),
            Preview = preview,
        });
    }

    public void OpenHistory() =>
        views.Open(new EditorViewRequest(HistoryId, Strings.LogTitle, () => new GitHistoryViewModel(repository, reader, diffs, shell)));
}
