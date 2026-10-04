using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.Git.ViewModels;

namespace CodeEditor.Modules.Git.Wpf.Views;

/// <summary>
/// git panel. Visual logic only: message box focus on <c>Ctrl+Shift+G</c>, list and box focus for context keys, row
/// clicks (preview, tab), right-click row selection, the "…" menu. Actions live in <see cref="GitPanelViewModel"/>.
/// </summary>
public sealed partial class GitPanelView
{
    private GitPanelViewModel? _panel;

    public GitPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // The side bar recreates the view on every show while the view model lives on: subscribe only while on screen.
        Loaded += (_, _) => Attach(DataContext as GitPanelViewModel);
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    // Showing the panel anywhere (or as an editor tab) refreshes state at once.
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            (DataContext as GitPanelViewModel)?.OnShown();
        }
    }

    // A hidden panel holds no focus, so Ctrl+Enter and Enter go back to other elements.
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _panel?.Commit.SetFocused(false);
        _panel?.Changes.SetFocused(false);
        Attach(null);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Attach(e.NewValue as GitPanelViewModel);

        // The view is created on panel show, after focus was already requested.
        if (_panel is not null)
        {
            OnFocusRequested(this, EventArgs.Empty);
        }
    }

    private void Attach(GitPanelViewModel? panel)
    {
        if (_panel is not null)
        {
            _panel.FocusRequested -= OnFocusRequested;
        }

        _panel = panel;
        if (_panel is not null)
        {
            _panel.FocusRequested += OnFocusRequested;
        }
    }

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (MessageInput.IsVisible)
            {
                Keyboard.Focus(MessageInput);
            }
        });

    private void OnMessageFocusChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _panel?.Commit.SetFocused(MessageInput.IsKeyboardFocusWithin);

    private void OnChangesFocusChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _panel?.Changes.SetFocused(Changes.IsKeyboardFocusWithin);

    /// <summary>The "…" menu also opens on left click, as in VS Code; right click works by itself.</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu)
        {
            return;
        }

        menu.DataContext = DataContext;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnMoreMenuOpened(object sender, RoutedEventArgs e) => _panel?.MoreMenu.Refresh();

    private void OnContextMenuOpened(object sender, RoutedEventArgs e) => _panel?.ContextMenu.Refresh();

    /// <summary>Right click selects the row so the context menu acts on it, as in VS Code.</summary>
    private void OnRowRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item && IsClickOnOwnRow(item, e))
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>A single click on a file previews its changes; a click on a row button only runs that button.</summary>
    private void OnChangeClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: GitChangeItemViewModel change } item && IsClickOnOwnRow(item, e) && !IsClickOnButton(e))
        {
            _panel?.PreviewChangesCommand.Execute(change);
        }
    }

    private void OnChangeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { DataContext: GitChangeItemViewModel change, IsSelected: true } && !IsClickOnButton(e))
        {
            e.Handled = true;
            _panel?.OpenChangesCommand.Execute(change);
        }
    }

    // The event bubbles through the group row; only the row under the pointer handles it.
    private static bool IsClickOnOwnRow(TreeViewItem item, MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null; element = ParentOf(element))
        {
            if (element is TreeViewItem owner)
            {
                return ReferenceEquals(owner, item);
            }
        }

        return false;
    }

    private static bool IsClickOnButton(MouseButtonEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null and not TreeViewItem; element = ParentOf(element))
        {
            if (element is ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    // A click on text comes from a Run, which is not in the visual tree; its parent is in the logical tree.
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
