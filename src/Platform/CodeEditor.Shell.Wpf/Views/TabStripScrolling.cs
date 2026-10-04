using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Tab strip scrolling, as in VS Code: the mouse wheel and its tilt scroll the strip sideways, and the shown tab scrolls
/// into view when it changes. Ctrl + wheel stays the interface zoom (the window handles it first).
/// </summary>
internal sealed class TabStripScrolling
{
    // WPF raises no event for the horizontal wheel (tilt): it comes as WM_MOUSEHWHEEL to the window.
    private const int HorizontalWheelMessage = 0x020E;
    private const int WheelDeltaShift = 16;

    private readonly ItemsControl _strip;
    private readonly ScrollViewer _scroller;
    private EditorGroupViewModel? _group;
    private HwndSource? _window;

    public TabStripScrolling(ItemsControl strip)
    {
        _strip = strip;
        _scroller = ScrollerOf(strip);
        _scroller.PreviewMouseWheel += OnPreviewMouseWheel;
        _strip.Loaded += (_, _) => OnLoaded();
        _strip.Unloaded += (_, _) => OnUnloaded();
        _strip.DataContextChanged += (_, _) => Attach(_strip.IsLoaded ? _strip.DataContext as EditorGroupViewModel : null);
    }

    private void OnLoaded()
    {
        Attach(_strip.DataContext as EditorGroupViewModel);
        if (_window is null && PresentationSource.FromVisual(_scroller) is HwndSource window)
        {
            _window = window;
            _window.AddHook(OnWindowMessage);
        }
    }

    // Unsubscribe while out of the tree, so a closed group isn't kept alive.
    private void OnUnloaded()
    {
        Attach(null);
        _window?.RemoveHook(OnWindowMessage);
        _window = null;
    }

    private void Attach(EditorGroupViewModel? group)
    {
        if (ReferenceEquals(group, _group))
        {
            return;
        }

        if (_group is not null)
        {
            _group.PropertyChanged -= OnGroupChanged;
        }

        _group = group;
        if (_group is not null)
        {
            _group.PropertyChanged += OnGroupChanged;
            RevealActive();
        }
    }

    private void OnGroupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorGroupViewModel.Active))
        {
            RevealActive();
        }
    }

    // After layout: a new tab has no container until the strip measures it.
    private void RevealActive() =>
        _strip.Dispatcher.InvokeAsync(
            () =>
            {
                if (_group?.Active is { } tab && _strip.ItemContainerGenerator.ContainerFromItem(tab) is FrameworkElement container)
                {
                    container.BringIntoView();
                }
            },
            DispatcherPriority.Loaded);

    // Wheel down moves on to the tabs on the right; a notch (120) is about one short tab.
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroller.ScrollableWidth > 0)
        {
            e.Handled = true;
            ScrollBy(-e.Delta);
        }
    }

    private nint OnWindowMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == HorizontalWheelMessage && _scroller.IsMouseOver && _scroller.ScrollableWidth > 0)
        {
            handled = true;
            ScrollBy((short)(wParam.ToInt64() >> WheelDeltaShift));
        }

        return 0;
    }

    private void ScrollBy(double offset) => _scroller.ScrollToHorizontalOffset(_scroller.HorizontalOffset + offset);

    // The logical tree exists right after InitializeComponent; the visual one only after templates apply.
    private static ScrollViewer ScrollerOf(FrameworkElement strip)
    {
        for (var parent = strip.Parent; parent is not null; parent = LogicalTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer scroller)
            {
                return scroller;
            }
        }

        throw new InvalidOperationException("The tab strip must be inside a ScrollViewer.");
    }
}
