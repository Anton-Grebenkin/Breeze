using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodeEditor.Shell.Wpf.Windowing;

/// <summary>
/// Native details of a custom-chrome window (<c>WindowChrome</c>): the maximized inset and rounded corners.
/// </summary>
internal static class WindowChromeInterop
{
    private const int SmCxFrame = 32;
    private const int SmCyFrame = 33;
    private const int SmCxPaddedBorder = 92;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCornerRound = 2;
    private const double DefaultDpi = 96;

    /// <summary>
    /// A maximized <c>WindowChrome</c> window extends past the screen by the frame thickness; content is inset by it.
    /// </summary>
    public static Thickness MaximizedInset(Visual visual)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        var systemDpi = (uint)(DefaultDpi * dpi.DpiScaleX);

        var padded = GetSystemMetricsForDpi(SmCxPaddedBorder, systemDpi);
        var horizontal = (GetSystemMetricsForDpi(SmCxFrame, systemDpi) + padded) / dpi.DpiScaleX;
        var vertical = (GetSystemMetricsForDpi(SmCyFrame, systemDpi) + padded) / dpi.DpiScaleY;

        return new Thickness(horizontal, vertical, horizontal, vertical);
    }

    /// <summary>Rounds corners on Windows 11; a silent no-op on Windows 10.</summary>
    public static void RequestRoundedCorners(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var preference = DwmCornerRound;
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
