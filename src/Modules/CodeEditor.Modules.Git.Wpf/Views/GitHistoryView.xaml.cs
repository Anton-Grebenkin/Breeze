namespace CodeEditor.Modules.Git.Wpf.Views;

/// <summary>
/// git history in an editor tab: commits on the left; the selected commit's message, files and diff on the right.
/// No code: selection and loading live in <c>GitHistoryViewModel</c>.
/// </summary>
public sealed partial class GitHistoryView
{
    public GitHistoryView() => InitializeComponent();
}
