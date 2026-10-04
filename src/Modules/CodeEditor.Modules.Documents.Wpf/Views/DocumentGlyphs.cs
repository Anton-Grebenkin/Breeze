using CodeEditor.UI.Themes;

namespace CodeEditor.Modules.Documents.Wpf.Views;

/// <summary>Viewer toolbar Codicons glyphs: <c>{x:Static views:DocumentGlyphs.OpenExternal}</c>.</summary>
public static class DocumentGlyphs
{
    public static string Refresh { get; } = IconGlyphs.Refresh;

    public static string OpenExternal { get; } = Codicons.Glyph("link-external");

    public static string OpenAsText { get; } = Codicons.Glyph("file-code");
}
