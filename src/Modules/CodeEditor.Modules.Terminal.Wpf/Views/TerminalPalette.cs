using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Shell.Theming;
using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Terminal.Wpf.Views;

/// <summary>Terminal colors from the current theme tokens; the page adds the ANSI palette of the scheme.</summary>
internal static class TerminalPalette
{
    public static TerminalTheme Read(FrameworkElement element, ThemeKind kind) => new(
        kind == ThemeKind.Light ? "light" : "dark",
        Css(ColorOf(element, ThemeKeys.EditorBackground)),
        Css(ColorOf(element, ThemeKeys.EditorForeground)),
        Css(ColorOf(element, ThemeKeys.FocusBorder)),
        Css(ColorOf(element, ThemeKeys.EditorSelection)));

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
