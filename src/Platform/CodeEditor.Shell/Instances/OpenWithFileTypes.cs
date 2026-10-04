using System.Collections.Immutable;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// Text and code files for which Windows offers Breeze in "Open with" and "Default apps". Registering them changes
/// nothing on its own: a file opens in Breeze by double-click only after the user picks Breeze for its type.
/// </summary>
public static class OpenWithFileTypes
{
    public static ImmutableArray<string> Extensions { get; } =
    [
        // Text and data
        ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".json", ".jsonc", ".xml", ".yml", ".yaml", ".toml", ".ini",
        ".cfg", ".conf", ".config", ".env", ".editorconfig", ".gitignore", ".gitattributes", ".mmd", ".http",

        // .NET
        ".cs", ".csx", ".csproj", ".sln", ".slnx", ".props", ".targets", ".vb", ".vbproj", ".fs", ".fsx", ".fsproj",
        ".xaml", ".axaml", ".razor", ".cshtml", ".resx", ".nuspec",

        // Web
        ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx", ".html", ".htm", ".css", ".scss", ".less", ".vue", ".svelte",

        // Scripts
        ".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".sh", ".bash",

        // Other languages
        ".py", ".c", ".h", ".cpp", ".hpp", ".go", ".rs", ".java", ".kt", ".php", ".rb", ".lua", ".sql", ".proto",
        ".graphql", ".diff", ".patch",
    ];
}
