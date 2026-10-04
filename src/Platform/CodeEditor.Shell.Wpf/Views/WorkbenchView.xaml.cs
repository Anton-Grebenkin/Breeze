using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// The workbench view. Visual logic only: opening Manage on left click and dragging tool windows, whose icon or tab can
/// be dropped on another area or on an editor group (ADR 0031). A drop target's <see cref="FrameworkElement.Tag"/>
/// holds the <see cref="ToolWindowLocation"/> name. Files dropped on the editor area: <c>WorkbenchView.FileDrop.cs</c>.
/// </summary>
public sealed partial class WorkbenchView
{
    private const string ToolWindowFormat = "CodeEditor.ToolWindow";

    private Point? _dragStart;

    public WorkbenchView() => InitializeComponent();

    private MainWindowViewModel? Main => DataContext as MainWindowViewModel;

    /// <summary>Manage opens on left click too, as in VS Code; right click works on its own.</summary>
    private void OnManageClick(object sender, RoutedEventArgs e)
    {
        if (ManageButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = ManageButton;
            menu.IsOpen = true;
        }
    }

    private void OnManageMenuOpened(object sender, RoutedEventArgs e) =>
        (ManageButton.DataContext as ActivityBarViewModel)?.Manage.Refresh();

    private void OnToolWindowMouseDown(object sender, MouseButtonEventArgs e) => _dragStart = e.GetPosition(this);

    // Moving a pressed tool window button past the drag threshold starts a move.
    private void OnToolWindowMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start
            || sender is not FrameworkElement { DataContext: ToolWindowViewModel toolWindow } element)
        {
            return;
        }

        var offset = e.GetPosition(this) - start;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        DragDrop.DoDragDrop(element, new DataObject(ToolWindowFormat, toolWindow), DragDropEffects.Move);
    }

    private void OnAreaDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ToolWindowFormat))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            OnFileDragOver(sender, e);
        }
    }

    private void OnAreaDrop(object sender, DragEventArgs e)
    {
        _dragStart = null;
        if (!e.Data.GetDataPresent(ToolWindowFormat))
        {
            OpenDroppedFiles(sender, e);
            return;
        }

        if (e.Data.GetData(ToolWindowFormat) is not ToolWindowViewModel toolWindow || Main is not { } main
            || sender is not FrameworkElement { Tag: string name } || !Enum.TryParse<ToolWindowLocation>(name, out var location))
        {
            return;
        }

        // Into the editor: the group under the drop point.
        if (location == ToolWindowLocation.Editor && GroupUnder(e.OriginalSource) is { } group)
        {
            main.EditorArea.Editors.Activate(group);
        }

        main.Layout.Move(toolWindow.Id, location);
        e.Handled = true;
    }

    private static EditorGroupViewModel? GroupUnder(object source)
    {
        for (var node = source as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is EditorGroupView { DataContext: EditorGroupViewModel group })
            {
                return group;
            }
        }

        return null;
    }
}
