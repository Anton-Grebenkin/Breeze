using System.Drawing;
using System.Runtime.InteropServices;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Which Windows window is at a screen point. A WebView2 page is a child window over WPF content: if it is at a WPF
/// element's point, the page covers the element and a click would go to the page.
/// </summary>
public static class WindowHitTest
{
    public static nint WindowAt(Point point) => WindowFromPoint(new NativePoint(point.X, point.Y));

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint WindowFromPoint(NativePoint point);
}
