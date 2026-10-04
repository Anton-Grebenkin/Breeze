using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Diagrams.Wpf.Views;

/// <summary>
/// Preview page colors from the current theme tokens. The page receives them with every state, so a theme change
/// repaints it together with the re-rendered diagrams. The page has no colors of its own.
/// </summary>
internal static class PreviewPalette
{
    public static IReadOnlyDictionary<string, string> Read(FrameworkElement element) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["background"] = Css(ColorOf(element, ThemeKeys.EditorBackground)),
        ["foreground"] = Css(ColorOf(element, ThemeKeys.EditorForeground)),
        ["secondary"] = Css(ColorOf(element, ThemeKeys.TextSecondary)),
        ["focus"] = Css(ColorOf(element, ThemeKeys.FocusBorder)),
    };

    /// <summary>The WebView2 background before the page loads, so the dark theme gets no white flash.</summary>
    public static System.Drawing.Color Background(FrameworkElement element)
    {
        var color = ColorOf(element, ThemeKeys.EditorBackground);
        return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static Color ColorOf(FrameworkElement element, string key) =>
        element.TryFindResource(key) is SolidColorBrush brush ? brush.Color : Colors.Transparent;

    private static string Css(Color color) => color.A == byte.MaxValue
        ? string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"rgba({color.R}, {color.G}, {color.B}, {color.A / (double)byte.MaxValue:0.###})");
}
