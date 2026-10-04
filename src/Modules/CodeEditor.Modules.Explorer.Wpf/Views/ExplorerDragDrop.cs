using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Shell.Wpf.Input;
using CodeEditor.UI.Controls;

namespace CodeEditor.Modules.Explorer.Wpf.Views;

/// <summary>
/// Drag and drop in the explorer tree; visual logic only, the file operations live in <see cref="ExplorerDrop"/>. A row
/// drags its path in the app's own format (<see cref="FileDragData"/>): the chat and the editor accept it, Windows
/// Explorer doesn't. The tree takes its own rows (move, or copy with <c>Ctrl</c>) and files from Windows (copy), not
/// editor tabs. The target folder is highlighted, a collapsed folder opens after a short hover and the tree scrolls
/// near its top and bottom edges.
/// </summary>
internal sealed class ExplorerDragDrop
{
    private static readonly TimeSpan ExpandDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>Height of the bands at the top and bottom edges that scroll the tree during a drag.</summary>
    private const double ScrollBand = 20;

    private readonly TreeView _tree;
    private readonly Func<ExplorerPanelViewModel?> _panel;
    private readonly DispatcherTimer _expandTimer;
    private FileNodeViewModel? _pressed;
    private Point _pressPoint;
    private string[]? _ownPaths;
    private DependencyObject? _highlighted;
    private FileNodeViewModel? _expandCandidate;
    private ScrollViewer? _scroller;

    private ExplorerDragDrop(TreeView tree, Func<ExplorerPanelViewModel?> panel)
    {
        _tree = tree;
        _panel = panel;
        _expandTimer = new DispatcherTimer(DispatcherPriority.Input, tree.Dispatcher) { Interval = ExpandDelay };
        _expandTimer.Tick += OnExpandTimerTick;

        tree.AllowDrop = true;
        tree.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
        tree.PreviewMouseMove += OnPreviewMouseMove;
        tree.DragEnter += OnDragOver;
        tree.DragOver += OnDragOver;
        tree.DragLeave += OnDragLeave;
        tree.Drop += OnDrop;
    }

    /// <summary>Wires drag and drop to the tree; the handlers live as long as the tree.</summary>
    public static void Attach(TreeView tree, Func<ExplorerPanelViewModel?> panel) => _ = new ExplorerDragDrop(tree, panel);

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = NodeAt(e.OriginalSource, out _) is { IsPlaceholder: false, IsEditing: false, IsPendingCreation: false } node ? node : null;
        _pressPoint = e.GetPosition(_tree);
    }

    // Past the system drag threshold a pressed row starts a drag; DoDragDrop returns when the drag ends.
    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressed is not { } node)
        {
            return;
        }

        var offset = e.GetPosition(_tree) - _pressPoint;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressed = null;
        if (node.IsEditing)
        {
            return;
        }

        var data = new DataObject();
        _ownPaths = [node.FullPath];
        FileDragData.SetPaths(data, _ownPaths);
        try
        {
            DragDrop.DoDragDrop(_tree, data, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            _ownPaths = null;
            EndFeedback();
        }
    }

    // Drags the tree doesn't take (a tool window icon, text) are left to the side bar around it.
    private void OnDragOver(object sender, DragEventArgs e)
    {
        var requested = Requested(e, out var paths);
        if (requested == ExplorerDropEffect.None)
        {
            EndFeedback();
            return;
        }

        e.Handled = true;
        var node = NodeAt(e.OriginalSource, out var item);
        var effect = Evaluate(e, paths, node, requested);
        e.Effects = ToDragEffects(effect);
        Highlight(effect == ExplorerDropEffect.None ? null : ContainerOfTarget(item, node));
        ScheduleExpand(node);
        ScrollNearEdges(e.GetPosition(_tree));
    }

    // Moving between rows raises leave and enter in pairs; only leaving the tree ends the feedback.
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(_tree);
        if (point.X < 0 || point.Y < 0 || point.X >= _tree.ActualWidth || point.Y >= _tree.ActualHeight)
        {
            EndFeedback();
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        EndFeedback();
        var requested = Requested(e, out var paths);
        if (requested == ExplorerDropEffect.None)
        {
            return;
        }

        e.Handled = true;
        var node = NodeAt(e.OriginalSource, out _);
        var effect = Evaluate(e, paths, node, requested);
        e.Effects = ToDragEffects(effect);
        if (effect == ExplorerDropEffect.None || _panel() is not { } panel)
        {
            return;
        }

        // After the drop returns: a replace question must not hold up the drag loop of Windows Explorer.
        _ = _tree.Dispatcher.InvokeAsync(() => panel.Drop.DropAsync(paths, node, effect));
    }

    private ExplorerDropEffect Evaluate(DragEventArgs e, string[] paths, FileNodeViewModel? node, ExplorerDropEffect requested)
    {
        var allowed = (e.AllowedEffects & ToDragEffects(requested)) != 0;
        return allowed && _panel() is { } panel ? panel.Drop.Evaluate(paths, node, requested) : ExplorerDropEffect.None;
    }

    // Own rows move, or copy with Ctrl; files from Windows are copied; tabs and other drags are not taken.
    private ExplorerDropEffect Requested(DragEventArgs e, out string[] paths)
    {
        if (_ownPaths is not null)
        {
            paths = _ownPaths;
            return (e.KeyStates & DragDropKeyStates.ControlKey) != 0 ? ExplorerDropEffect.Copy : ExplorerDropEffect.Move;
        }

        paths = e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files ? files : [];
        return paths.Length > 0 ? ExplorerDropEffect.Copy : ExplorerDropEffect.None;
    }

    private static DragDropEffects ToDragEffects(ExplorerDropEffect effect) => effect switch
    {
        ExplorerDropEffect.Move => DragDropEffects.Move,
        ExplorerDropEffect.Copy => DragDropEffects.Copy,
        _ => DragDropEffects.None,
    };

    // The target folder's container: a folder row with its children, a file row's parent, the whole tree for the root.
    private DependencyObject ContainerOfTarget(TreeViewItem? item, FileNodeViewModel? node)
    {
        if (item is null)
        {
            return _tree;
        }

        var isTarget = _panel() is { } panel && ReferenceEquals(panel.Drop.TargetOf(node), node);
        return isTarget ? item : ItemsControl.ItemsControlFromItemContainer(item) ?? (DependencyObject)_tree;
    }

    private void Highlight(DependencyObject? element)
    {
        if (ReferenceEquals(element, _highlighted))
        {
            return;
        }

        _highlighted?.ClearValue(DropHighlight.IsActiveProperty);
        _highlighted = element;
        _highlighted?.SetValue(DropHighlight.IsActiveProperty, true);
    }

    private void ScheduleExpand(FileNodeViewModel? node)
    {
        var candidate = node is { IsDirectory: true, IsExpanded: false, IsPlaceholder: false, IsPendingCreation: false } ? node : null;
        if (ReferenceEquals(candidate, _expandCandidate))
        {
            return;
        }

        _expandTimer.Stop();
        _expandCandidate = candidate;
        if (candidate is not null)
        {
            _expandTimer.Start();
        }
    }

    private void OnExpandTimerTick(object? sender, EventArgs e)
    {
        _expandTimer.Stop();
        _expandCandidate?.IsExpanded = true;
        _expandCandidate = null;
    }

    // OLE repeats DragOver while the cursor rests, so holding it at an edge keeps scrolling.
    private void ScrollNearEdges(Point point)
    {
        _scroller ??= FindDescendant<ScrollViewer>(_tree);
        if (point.Y < ScrollBand)
        {
            _scroller?.LineUp();
        }
        else if (point.Y > _tree.ActualHeight - ScrollBand)
        {
            _scroller?.LineDown();
        }
    }

    private void EndFeedback()
    {
        Highlight(null);
        _expandTimer.Stop();
        _expandCandidate = null;
    }

    /// <summary>The node of the row under the element; <c>null</c> for empty space.</summary>
    private static FileNodeViewModel? NodeAt(object source, out TreeViewItem? item)
    {
        for (var element = source as DependencyObject; element is not null and not TreeView; element = ParentOf(element))
        {
            if (element is TreeViewItem row)
            {
                item = row;
                return row.DataContext as FileNodeViewModel;
            }
        }

        item = null;
        return null;
    }

    // Text runs are content elements outside the visual tree.
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if ((child as T ?? FindDescendant<T>(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
