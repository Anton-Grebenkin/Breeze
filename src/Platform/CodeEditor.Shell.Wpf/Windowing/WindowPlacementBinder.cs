using System.Windows;
using CodeEditor.Shell.Layout;

namespace CodeEditor.Shell.Wpf.Windowing;

/// <summary>
/// Copies the saved placement to a WPF window and back. A placement off all screens (e.g. after unplugging a monitor)
/// is ignored and the window opens centered.
/// </summary>
internal static class WindowPlacementBinder
{
    /// <summary>How much of the window, in DIPs, must be on screen for the placement to be used.</summary>
    private const double MinVisiblePart = 100;

    public static void Apply(Window window, WindowPlacement? placement)
    {
        if (placement is null || !IsOnScreen(placement))
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = placement.Left;
        window.Top = placement.Top;
        window.Width = Math.Max(placement.Width, window.MinWidth);
        window.Height = Math.Max(placement.Height, window.MinHeight);
        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>For a maximized window, saves the normal bounds so there is something to restore to.</summary>
    public static WindowPlacement Capture(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        return new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, window.WindowState == WindowState.Maximized);
    }

    private static bool IsOnScreen(WindowPlacement placement)
    {
        var screen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var window = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var visible = Rect.Intersect(screen, window);
        return !visible.IsEmpty && visible.Width >= MinVisiblePart && visible.Height >= MinVisiblePart;
    }
}
