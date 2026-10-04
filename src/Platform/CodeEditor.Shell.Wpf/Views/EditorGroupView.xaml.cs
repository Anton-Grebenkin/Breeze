using System.Windows;
using System.Windows.Input;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Editor tab group. Click activates a tab, double click pins a preview, middle click closes; focus inside makes the
/// group active; dragging a tab reorders it, moves it to another group, or onto an edge to create a new group (ADR 0031).
/// Dragged files and the file of a dragged tab: <c>EditorGroupView.FileDrop.cs</c>.
/// </summary>
public sealed partial class EditorGroupView
{
    public static readonly DependencyProperty HostProperty =
        DependencyProperty.Register(nameof(Host), typeof(EditorAreaHost), typeof(EditorGroupView));

    private const string TabFormat = "CodeEditor.EditorTab";

    /// <summary>Width share at each edge where dropping a tab creates a new group.</summary>
    private const double EdgeShare = 0.25;

    private Point? _dragStart;
    private EditorTab? _dragTab;

    public EditorGroupView()
    {
        InitializeComponent();
        IsKeyboardFocusWithinChanged += OnFocusWithinChanged;
        _ = new TabStripScrolling(TabStrip);
        // The code editor (AvalonEdit) marks DragLeave handled: without handledEventsToo the drop hint stayed on screen
        // after a file was dragged over the editor into the chat.
        EditorContent.AddHandler(DragLeaveEvent, new DragEventHandler(OnContentDragLeave), handledEventsToo: true);
    }

    private enum DropZone
    {
        Left,
        Center,
        Right,
    }

    /// <summary>The editor area: tab commands and the context menu.</summary>
    public EditorAreaHost? Host
    {
        get => (EditorAreaHost?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    private EditorAreaViewModel? Area => Host?.Editors;

    private EditorGroupViewModel? Group => DataContext as EditorGroupViewModel;

    // Focus inside makes the group active: files open in it and commands act on its tab.
    private void OnFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsKeyboardFocusWithin && Group is { } group)
        {
            Area?.Activate(group);
        }
    }

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EditorTab tab })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            tab.IsPreview = false;
        }

        Area?.Activate(tab);
        (_dragStart, _dragTab) = (e.GetPosition(this), tab);
    }

    private void OnTabMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragTab is not { } tab || _dragStart is not { } start)
        {
            return;
        }

        var offset = e.GetPosition(this) - start;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        (_dragStart, _dragTab) = (null, null);
        StartTabDrag((DependencyObject)sender, tab);
    }

    /// <summary>Right click activates the tab so context menu commands act on it.</summary>
    private void OnTabRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EditorTab tab })
        {
            Area?.Activate(tab);
            Host?.TabContextMenu.Refresh();
        }
    }

    private void OnTabMouseUp(object sender, MouseButtonEventArgs e)
    {
        (_dragStart, _dragTab) = (null, null);
        if (e.ChangedButton == MouseButton.Middle && sender is FrameworkElement { DataContext: EditorTab tab })
        {
            e.Handled = true;
            _ = Area?.CloseAsync(tab);
        }
    }

    private void OnTabStripDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(TabFormat) ? DragDropEffects.Move : FileDropEffect(e);
        e.Handled = true;
    }

    // Tab strip drop: reorder or move from another group to the position under the cursor.
    private void OnTabStripDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(TabFormat) is EditorTab tab && Group is { } group)
        {
            Area?.MoveToGroup(tab, group, IndexAt(e.GetPosition(TabStrip)));
            e.Handled = true;
        }
        else if (!e.Data.GetDataPresent(TabFormat))
        {
            OpenDroppedFiles(e, DropZone.Center);
        }
    }

    private int IndexAt(Point point)
    {
        for (var index = 0; index < TabStrip.Items.Count; index++)
        {
            if (TabStrip.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container
                && point.X < container.TranslatePoint(new Point(container.ActualWidth / 2, 0), TabStrip).X)
            {
                return index;
            }
        }

        return TabStrip.Items.Count;
    }

    private void OnContentDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TabFormat))
        {
            OnFileDragOver(e);
            return;
        }

        e.Effects = DragDropEffects.Move;
        ShowHint(ZoneAt(e.GetPosition(EditorContent)));
        e.Handled = true;
    }

    // Also raised when the pointer moves between children; the next DragOver shows the hint again.
    private void OnContentDragLeave(object sender, DragEventArgs e) => HideHint();

    // Center drops into this group; an edge creates a new group beside it.
    private void OnContentDrop(object sender, DragEventArgs e)
    {
        HideHint();
        if (!e.Data.GetDataPresent(TabFormat))
        {
            OpenDroppedFiles(e, ZoneAt(e.GetPosition(EditorContent)));
            return;
        }

        if (e.Data.GetData(TabFormat) is not EditorTab tab || Group is not { } group || Area is not { } area)
        {
            return;
        }

        var zone = ZoneAt(e.GetPosition(EditorContent));
        var target = zone == DropZone.Center ? group : area.AddGroup(group, before: zone == DropZone.Left);
        if (target is not null)
        {
            area.MoveToGroup(tab, target);
        }

        e.Handled = true;
    }

    private DropZone ZoneAt(Point point) =>
        point.X < EditorContent.ActualWidth * EdgeShare ? DropZone.Left
        : point.X > EditorContent.ActualWidth * (1 - EdgeShare) ? DropZone.Right
        : DropZone.Center;

    private void ShowHint(DropZone zone)
    {
        DropHint.HorizontalAlignment = zone switch
        {
            DropZone.Left => HorizontalAlignment.Left,
            DropZone.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Stretch,
        };
        DropHint.Width = zone == DropZone.Center ? double.NaN : EditorContent.ActualWidth / 2;
        DropHint.Visibility = Visibility.Visible;
    }

    private void HideHint() => DropHint.Visibility = Visibility.Collapsed;
}
