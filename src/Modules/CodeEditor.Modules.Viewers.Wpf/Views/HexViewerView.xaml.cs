using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CodeEditor.Modules.Viewers.ViewModels;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// Hex view. Visual logic: the column header follows the list's horizontal scroll, and go to offset centers the row in
/// the window and focuses it. Tab visibility goes to the view model: the first show reads the file length.
/// </summary>
public sealed partial class HexViewerView
{
    private HexViewerViewModel? _viewModel;

    public HexViewerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => _viewModel?.SetShown(IsVisible);
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => HeaderShift.X = -e.HorizontalOffset));
    }

    private void Attach()
    {
        var viewModel = IsLoaded ? DataContext as HexViewerViewModel : null;
        if (ReferenceEquals(viewModel, _viewModel))
        {
            return;
        }

        Detach();
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.ScrollRequested += OnScrollRequested;
            _viewModel.SetShown(IsVisible);
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.ScrollRequested -= OnScrollRequested;
            _viewModel = null;
        }
    }

    // The list scrolls in rows (CanContentScroll): put the row in the middle of the window.
    private void OnScrollRequested(object? sender, int row)
    {
        if (FindScroller(Rows) is { } scroller)
        {
            scroller.ScrollToVerticalOffset(Math.Max(0, row - (scroller.ViewportHeight / 2)));
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => (Rows.ItemContainerGenerator.ContainerFromIndex(row) as ListBoxItem)?.Focus());
    }

    private static ScrollViewer? FindScroller(Visual parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer scroller)
            {
                return scroller;
            }

            if (child is Visual visual && FindScroller(visual) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
