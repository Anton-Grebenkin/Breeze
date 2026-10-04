using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.Explorer.ViewModels;

namespace CodeEditor.Modules.Explorer.Wpf.Views;

/// <summary>
/// Explorer. Visual logic only: tree focus, double click, right-click selection, focus and keys of the name box; drag
/// and drop in <see cref="ExplorerDragDrop"/>. Operations live in <see cref="ExplorerPanelViewModel"/>.
/// </summary>
public sealed partial class ExplorerView
{
    public ExplorerView()
    {
        InitializeComponent();
        ExplorerDragDrop.Attach(Tree, () => Panel);
    }

    private ExplorerPanelViewModel? Panel => DataContext as ExplorerPanelViewModel;

    private void OnTreeFocusChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        Panel?.SetFocused(Tree.IsKeyboardFocusWithin);

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // The event bubbles through all parent rows; handle only the row under the cursor.
        if (sender is TreeViewItem { DataContext: FileNodeViewModel { IsDirectory: false } node } item && item.IsSelected)
        {
            e.Handled = true;
            Panel?.OpenCommand.Execute(node);
        }
    }

    /// <summary>A single click on a file opens a preview; arrow keys don't open files.</summary>
    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: FileNodeViewModel { IsDirectory: false, IsEditing: false } node } item
            && IsClickOnOwnRow(item, e))
        {
            Panel?.PreviewCommand.Execute(node);
        }
    }

    /// <summary>Right click selects the row so the context menu acts on it, as in VS Code.</summary>
    private void OnItemRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && IsClickOnOwnRow(item, e))
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e) => Panel?.ContextMenu.Refresh();

    private void OnEditBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox { IsVisible: true } box)
        {
            return;
        }

        // As in VS Code, rename selects the name without the extension.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            box.Focus();
            var extension = Path.GetExtension(box.Text);
            box.Select(0, extension.Length > 0 && extension.Length < box.Text.Length ? box.Text.Length - extension.Length : box.Text.Length);
        });
    }

    private void OnEditBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Panel?.CommitEditCommand.Execute(null);
            Tree.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Panel?.CancelEditCommand.Execute(null);
            Tree.Focus();
        }
    }

    private void OnEditBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true })
        {
            Panel?.CommitOrCancelEditCommand.Execute(null);
        }
    }

    private static bool IsClickOnOwnRow(TreeViewItem item, MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TreeViewItem owner)
            {
                return ReferenceEquals(owner, item);
            }
        }

        return false;
    }
}
