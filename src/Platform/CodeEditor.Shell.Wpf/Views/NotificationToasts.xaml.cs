using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Opens the notification popup while there are notifications and the window is active: a popup is a topmost window
/// and would otherwise float over other apps and a minimized Breeze. It doesn't follow its window either, so moves
/// and resizes reposition it.
/// </summary>
public sealed partial class NotificationToasts
{
    private const double Identity = 1;
    private const double Tolerance = 0.001;

    private Window? _window;
    private NotificationsViewModel? _notifications;

    public NotificationToasts()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => Reposition();
        DataContextChanged += (_, _) => Watch(DataContext as NotificationsViewModel);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.Activated += OnWindowChanged;
            _window.Deactivated += OnWindowChanged;
            _window.StateChanged += OnWindowChanged;
            _window.LocationChanged += OnWindowMoved;
        }

        Watch(DataContext as NotificationsViewModel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.Activated -= OnWindowChanged;
            _window.Deactivated -= OnWindowChanged;
            _window.StateChanged -= OnWindowChanged;
            _window.LocationChanged -= OnWindowMoved;
            _window = null;
        }

        Watch(null);
    }

    private void Watch(NotificationsViewModel? notifications)
    {
        if (_notifications is not null)
        {
            _notifications.Items.CollectionChanged -= OnItemsChanged;
        }

        _notifications = notifications;
        if (_notifications is not null)
        {
            _notifications.Items.CollectionChanged += OnItemsChanged;
        }

        UpdateOpen();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateOpen();

    private void OnWindowChanged(object? sender, EventArgs e) => UpdateOpen();

    private void OnWindowMoved(object? sender, EventArgs e) => Reposition();

    private void UpdateOpen()
    {
        var open = _notifications is { Items.Count: > 0 }
            && _window is { IsActive: true, WindowState: not WindowState.Minimized };
        if (open)
        {
            ApplyZoom();
        }

        Host.IsOpen = open;
    }

    // Popup content is outside the window's visual tree, so the interface zoom is applied to it separately.
    private void ApplyZoom()
    {
        var scale = _window is null ? Identity : TransformToAncestor(_window).TransformBounds(new Rect(0, 0, 1, 1)).Width;
        Toasts.LayoutTransform = Math.Abs(scale - Identity) < Tolerance ? Transform.Identity : new ScaleTransform(scale, scale);
    }

    private void Reposition()
    {
        if (!Host.IsOpen)
        {
            return;
        }

        // Changing the offset makes the popup recompute its position from the anchor.
        Host.HorizontalOffset += 1;
        Host.HorizontalOffset -= 1;
    }
}
