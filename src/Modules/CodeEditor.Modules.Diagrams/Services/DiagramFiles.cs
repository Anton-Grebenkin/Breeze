using System.Collections.Frozen;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// Diagram files: a <c>.mmd</c> or <c>.mermaid</c> file is one diagram (opened in the regular text editor); a Markdown
/// file holds <c>```mermaid</c> blocks in order (<see cref="MarkdownMermaidBlocks"/>).
/// </summary>
public static class DiagramFiles
{
    private static readonly FrozenSet<string> MermaidExtensions = FrozenSet.ToFrozenSet([".mmd", ".mermaid"], StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> MarkdownExtensions = FrozenSet.ToFrozenSet([".md", ".markdown"], StringComparer.OrdinalIgnoreCase);

    public static bool IsMermaid(string path) => MermaidExtensions.Contains(Path.GetExtension(path));

    public static bool IsMarkdown(string path) => MarkdownExtensions.Contains(Path.GetExtension(path));

    /// <summary>The file may contain diagrams, so it has a preview and export.</summary>
    public static bool CanContainDiagrams(string path) => IsMermaid(path) || IsMarkdown(path);

    /// <summary>The file's diagrams in order; an empty Mermaid file has none.</summary>
    public static IReadOnlyList<DiagramSource> Extract(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsMarkdown(path))
        {
            return MarkdownMermaidBlocks.Find(text);
        }

        return string.IsNullOrWhiteSpace(text) ? [] : [new DiagramSource(text, FirstLine: 1, Index: 1)];
    }
}
