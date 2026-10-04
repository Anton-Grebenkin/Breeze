using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Diagrams.Wpf.Views;

/// <summary>Preview toolbar icons as Codicons glyphs for markup: <c>{x:Static views:PreviewIcons.ZoomIn}</c>.</summary>
public static class PreviewIcons
{
    public static string ZoomIn { get; } = Codicons.Glyph("zoom-in");

    public static string ZoomOut { get; } = Codicons.Glyph("zoom-out");

    public static string Fit { get; } = Codicons.Glyph("screen-full");
}
