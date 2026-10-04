using System.Windows.Input;
using CodeEditor.Modules.Git.ViewModels.Tabs;

namespace CodeEditor.Modules.Git.Wpf.Views;

/// <summary>
/// Diff in an editor tab. Visual logic only: <c>Ctrl+C</c> and Edit → Copy pass the selected rows to
/// <see cref="GitDiffViewModel"/>.
/// </summary>
public sealed partial class GitDiffView
{
    public GitDiffView() => InitializeComponent();

    private void OnCanCopy(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = Lines.SelectedItems.Count > 0;
        e.Handled = true;
    }

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        (DataContext as GitDiffViewModel)?.CopyCommand.Execute(Lines.SelectedItems);
        e.Handled = true;
    }
}
