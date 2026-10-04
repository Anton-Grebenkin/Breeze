using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Wpf.Input;
using CodeEditor.Shell.Wpf.Windowing;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// The main window. It only composes the parts; logic lives in <see cref="MainWindowViewModel"/>. Visual details here:
/// custom chrome, window placement, interface zoom, lazy palette creation.
/// </summary>
public sealed partial class MainWindow
{
    private static readonly Thickness NormalBorder = new(1);

    private readonly KeyboardRouter _keyboardRouter;
    private readonly CommandPaletteViewModel _palette;
    private readonly WorkbenchLayout _layout;
    private readonly IReadOnlyList<IShutdownGuard> _shutdownGuards;
    private readonly AirspaceGuard _airspace = new();
    private readonly AppRestart _restart;
    private readonly WindowZoomBinder _zoom;
    private bool _closeConfirmed;

    public MainWindow(MainWindowViewModel viewModel, KeyboardRouter keyboardRouter, IEnumerable<IShutdownGuard> shutdownGuards, AppRestart restart)
    {
        _shutdownGuards = [.. shutdownGuards];
        _restart = restart;

        // Load the layout before the markup; by now all modules have declared their tool windows.
        _layout = viewModel.Layout;
        _layout.Load();

        DataContext = viewModel;
        InitializeComponent();
        WindowPlacementBinder.Apply(this, _layout.Window);
        _zoom = new WindowZoomBinder(this, ZoomRoot, viewModel.Zoom);

        _keyboardRouter = keyboardRouter;
        _keyboardRouter.Attach(this);

        _palette = viewModel.Palette;
        _palette.PropertyChanged += OnPalettePropertyChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowChromeInterop.RequestRoundedCorners(this);

        // A window maximized from the saved layout gets no OnStateChanged, so apply the inset now.
        UpdateMaximizedInset();
    }

    /// <summary>Another monitor means another frame thickness.</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateMaximizedInset();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Without a focused element the window receives no key presses.
        Keyboard.Focus(this);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateMaximizedInset();
    }

    /// <summary>
    /// A maximized custom-chrome window extends past the screen by the frame thickness; content is inset so the
    /// caption buttons and status bar aren't clipped.
    /// </summary>
    private void UpdateMaximizedInset()
    {
        var maximized = WindowState == WindowState.Maximized;
        Root.Margin = maximized ? WindowChromeInterop.MaximizedInset(this) : default;
        Root.BorderThickness = maximized ? default : NormalBorder;
    }

    /// <summary>
    /// Cancels the first close and asks the modules (unsaved files); if all agree, closes again without asking.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel)
        {
            return;
        }

        if (!_closeConfirmed)
        {
            e.Cancel = true;
            _ = ConfirmCloseAsync();
            return;
        }

        _layout.Window = WindowPlacementBinder.Capture(this);
        _layout.Save();
    }

    private async Task ConfirmCloseAsync()
    {
        foreach (var guard in _shutdownGuards)
        {
            if (!await guard.CanShutdownAsync())
            {
                // The user stays in the editor, so a pending restart is cancelled too.
                _restart.Cancel();
                return;
            }
        }

        // Close again after the Closing handler returns: WPF forbids Close() inside it.
        _closeConfirmed = true;
        await Dispatcher.InvokeAsync(Close, System.Windows.Threading.DispatcherPriority.Normal);
    }

    protected override void OnClosed(EventArgs e)
    {
        _palette.PropertyChanged -= OnPalettePropertyChanged;
        _keyboardRouter.Detach(this);
        _zoom.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// The palette is created on first open since the first frame doesn't need it. While it is open, WebView2 pages are
    /// hidden so they don't cover it (<see cref="AirspaceGuard"/>).
    /// </summary>
    private void OnPalettePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen))
        {
            return;
        }

        if (!_palette.IsOpen)
        {
            _airspace.Restore();
            return;
        }

        PaletteHost.Content ??= new CommandPaletteView { DataContext = _palette };
        _airspace.Hide(this);
    }
}
