using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.Docker.ViewModels;
using CodeEditor.Modules.Docker.ViewModels.Tree;

namespace CodeEditor.Modules.Docker.Wpf.Views;

/// <summary>
/// The Docker panel. Visual logic only: panel visibility (state refreshes while visible), tree selection and focus,
/// right click selects a row, double click runs the row action. Actions live in <see cref="DockerViewModel"/>.
/// </summary>
public sealed partial class DockerView
{
    private DockerViewModel? _viewModel;

    public DockerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested -= OnFocusRequested;
            _ = _viewModel.SetVisible(false);
        }

        _viewModel = e.NewValue as DockerViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusRequested += OnFocusRequested;
            _ = _viewModel.SetVisible(IsVisible);

            // The view is created on first panel show, after focus was already requested.
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => _ = _viewModel?.SetVisible(IsVisible);

    /// <summary>Shown by a command: focus the tree, or Refresh when Docker is unavailable.</summary>
    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (Tree.IsVisible)
            {
                Tree.Focus();
            }
            else if (Retry.IsVisible)
            {
                Retry.Focus();
            }
        });

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Selected = e.NewValue as DockerNodeViewModel;
        }
    }

    private void OnTreeFocusChanged(object sender, DependencyPropertyChangedEventArgs e) => _viewModel?.SetFocused(Tree.IsKeyboardFocusWithin);

    private void OnContextMenuOpened(object sender, RoutedEventArgs e) => _viewModel?.PrepareContextMenu();

    /// <summary>Right click selects the row so the context menu acts on it, as in VS Code.</summary>
    private void OnItemRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && IsClickOnOwnRow(item, e))
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>Double click on a leaf row opens logs or details; rows with children expand as usual.</summary>
    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // The event passes through all parent rows; only the row under the pointer handles it.
        if (sender is TreeViewItem { DataContext: DockerNodeViewModel { Children.Count: 0 } node } item && item.IsSelected && IsClickOnOwnRow(item, e))
        {
            e.Handled = true;
            _viewModel?.OpenCommand.Execute(node);
        }
    }

    private static bool IsClickOnOwnRow(TreeViewItem item, MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = ParentOf(element))
        {
            if (element is TreeViewItem owner)
            {
                return ReferenceEquals(owner, item);
            }

            // A button or link in the row has its own action; it is not a row click.
            if (element is ButtonBase)
            {
                return false;
            }
        }

        return false;
    }

    // A click on row text comes from a Run, which is not in the visual tree; its parent is in the logical tree.
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
