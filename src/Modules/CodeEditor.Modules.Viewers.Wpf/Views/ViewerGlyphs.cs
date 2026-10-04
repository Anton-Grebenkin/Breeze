using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>Viewer toolbar glyphs from Codicons: <c>{x:Static views:ViewerGlyphs.ZoomIn}</c>.</summary>
public static class ViewerGlyphs
{
    public static string Refresh { get; } = IconGlyphs.Refresh;

    public static string OpenExternal { get; } = Codicons.Glyph("link-external");

    public static string OpenAsText { get; } = Codicons.Glyph("file-code");

    public static string ZoomIn { get; } = Codicons.Glyph("zoom-in");

    public static string ZoomOut { get; } = Codicons.Glyph("zoom-out");

    public static string Fit { get; } = Codicons.Glyph("screen-full");

    public static string GoToOffset { get; } = Codicons.Glyph("target");
}
