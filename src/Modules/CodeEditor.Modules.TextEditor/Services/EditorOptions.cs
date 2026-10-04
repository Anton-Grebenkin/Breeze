namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// The <c>editor</c> settings section with VS Code keys: <c>"editor.fontSize": 16</c>, <c>"editor.tabSize": 2</c>,
/// <c>"editor.wordWrap": "on"</c>, <c>"editor.lineNumbers": "off"</c>, <c>"editor.renderWhitespace": "all"</c>.
/// </summary>
public sealed class EditorOptions
{
    public const string Section = "editor";

    /// <summary>Comma-separated fonts, first installed wins; empty means the theme font.</summary>
    public string? FontFamily { get; set; }

    public double FontSize { get; set; } = EditorSettings.DefaultFontSize;

    /// <summary>Points added to <see cref="FontSize"/> by font zoom (Ctrl + wheel over the editor); 0 without zoom.</summary>
    public double FontZoom { get; set; }

    public int TabSize { get; set; } = 4;

    public bool InsertSpaces { get; set; } = true;

    /// <summary><c>off</c> or <c>on</c>.</summary>
    public string WordWrap { get; set; } = "off";

    /// <summary><c>on</c> or <c>off</c>.</summary>
    public string LineNumbers { get; set; } = "on";

    /// <summary><c>none</c> or <c>all</c>.</summary>
    public string RenderWhitespace { get; set; } = "none";
}
