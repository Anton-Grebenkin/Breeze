using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CodeEditor.Shell.Theming;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// SVG and player page colors from the current theme tokens: the page gets them with every show, so a theme change
/// recolors it along with the editor. Pages have no colors of their own; <c>scheme</c> also recolors the player controls.
/// </summary>
internal static class PagePalette
{
    public static IReadOnlyDictionary<string, string> Read(FrameworkElement element, ThemeKind theme) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["scheme"] = theme == ThemeKind.Light ? "light" : "dark",
        ["background"] = Css(ColorOf(element, ThemeKeys.EditorBackground)),
        ["foreground"] = Css(ColorOf(element, ThemeKeys.EditorForeground)),
        ["secondary"] = Css(ColorOf(element, ThemeKeys.TextSecondary)),
        ["focus"] = Css(ColorOf(element, ThemeKeys.FocusBorder)),
        ["checkerBackground"] = Css(ColorOf(element, ThemeKeys.CheckerboardBackground)),
        ["checkerSquare"] = Css(ColorOf(element, ThemeKeys.CheckerboardSquare)),
    };

    /// <summary>The WebView2 background before the page loads, avoiding a white flash in a dark theme.</summary>
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
