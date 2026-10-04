using System.Collections.Frozen;
using CodeEditor.Shell;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>File icons by extension: Codicon names, as in VS Code's explorer without an icon theme.</summary>
public static class FileIcons
{
    public const string FolderClosed = IconNames.Folder;
    public const string FolderOpen = IconNames.FolderOpened;
    public const string Generic = IconNames.File;

    // Span lookup: no extension string is allocated per tree node.
    private static readonly FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> ByExtension =
        BuildMap().GetAlternateLookup<ReadOnlySpan<char>>();

    public static string ForFile(string name) =>
        ByExtension.TryGetValue(Path.GetExtension(name.AsSpan()), out var icon) ? icon : Generic;

    private static FrozenDictionary<string, string> BuildMap()
    {
        string[] code =
        [
            ".cs", ".csx", ".xaml", ".razor", ".vb", ".fs", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".go", ".rs",
            ".c", ".cpp", ".h", ".hpp", ".css", ".scss", ".html", ".htm", ".xml", ".yml", ".yaml", ".sql",
            ".ps1", ".psm1", ".sh", ".cmd", ".bat", ".csproj", ".slnx", ".sln", ".props", ".targets", ".toml",
        ];
        string[] images = [".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".svg", ".webp"];
        string[] audio = [".mp3", ".wav", ".ogg", ".oga", ".flac", ".m4a", ".aac", ".opus"];
        string[] video = [".mp4", ".webm", ".ogv", ".mov", ".m4v"];
        string[] documents = [".txt", ".log", ".rtf", ".docx", ".doc", ".odt", ".editorconfig", ".gitignore"];
        string[] tables = [".xlsx", ".xlsm", ".xls", ".ods", ".csv"];

        return code.Select(extension => (Extension: extension, Icon: IconNames.FileCode))
            .Concat(images.Select(extension => (Extension: extension, Icon: IconNames.FileMedia)))
            .Concat(audio.Select(extension => (Extension: extension, Icon: IconNames.Audio)))
            .Concat(video.Select(extension => (Extension: extension, Icon: IconNames.Video)))
            .Concat(documents.Select(extension => (Extension: extension, Icon: IconNames.FileText)))
            .Concat(tables.Select(extension => (Extension: extension, Icon: IconNames.Table)))
            .Concat(new[] { ".pptx", ".ppt", ".odp" }.Select(extension => (Extension: extension, Icon: IconNames.Presentation)))
            .Concat(new[] { ".mmd", ".mermaid" }.Select(extension => (Extension: extension, Icon: IconNames.Diagram)))
            .Append((Extension: ".pdf", Icon: IconNames.FilePdf))
            .Append((Extension: ".md", Icon: IconNames.Markdown))
            .Append((Extension: ".json", Icon: IconNames.Json))
            .ToFrozenDictionary(pair => pair.Extension, pair => pair.Icon, StringComparer.OrdinalIgnoreCase);
    }
}
