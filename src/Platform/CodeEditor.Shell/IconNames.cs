namespace CodeEditor.Shell;

/// <summary>
/// Codicon names for modules: a tool window or file names an icon, the view maps it to a font glyph. Only the icons in
/// use are listed; see https://microsoft.github.io/vscode-codicons/dist/codicon.html for all.
/// </summary>
public static class IconNames
{
    public const string Explorer = "files";
    public const string Search = "search";
    public const string Chat = "comment-discussion";
    public const string Output = "output";
    public const string Terminal = "terminal";
    public const string Browser = "globe";

    public const string Folder = "folder";
    public const string FolderOpened = "folder-opened";
    public const string File = "file";
    public const string FileCode = "file-code";
    public const string FileMedia = "file-media";
    public const string FileText = "file-text";
    public const string Markdown = "markdown";
    public const string Json = "json";
    public const string FilePdf = "file-pdf";

    /// <summary>Spreadsheets: Excel, CSV.</summary>
    public const string Table = "table";

    /// <summary>PowerPoint presentations.</summary>
    public const string Presentation = "preview";

    /// <summary>Mermaid diagrams.</summary>
    public const string Diagram = "type-hierarchy";

    /// <summary>Audio: MP3, WAV, FLAC and others.</summary>
    public const string Audio = "music";

    /// <summary>Video: MP4, WebM, MOV.</summary>
    public const string Video = "device-camera-video";
}
