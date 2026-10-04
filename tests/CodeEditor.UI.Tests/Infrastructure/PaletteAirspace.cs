using System.Drawing;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// WebView2 pages (browser, PDF, SVG, media) are native windows over WPF, and the palette over such a tab must not go
/// under the page (ADR 0031). The editor window, not the page window, must be under points of the palette list.
/// </summary>
public static class PaletteAirspace
{
    private static readonly double[] Shares = [0.25, 0.5, 0.75];

    public static void AssertPaletteIsNotCovered(AppSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var window = session.MainWindow.Properties.NativeWindowHandle.Value;
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        var palette = session.WaitFor("CommandPalette.Results").BoundingRectangle;
        var points = Shares.Select(share => new Point(palette.Left + palette.Width / 2, palette.Top + (int)(palette.Height * share)));
        Assert.All(points, point => Assert.Equal(window, WindowHitTest.WindowAt(point)));
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        session.WaitUntilGone("CommandPalette.Query");
    }
}
