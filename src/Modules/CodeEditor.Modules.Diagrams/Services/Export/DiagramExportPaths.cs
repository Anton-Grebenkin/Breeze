using System.Globalization;

namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>
/// Where a diagram is exported, next to the source: <c>docs/arch.mmd</c> → <c>docs/arch.svg</c>; Markdown blocks get a
/// number: <c>docs/README.md</c> → <c>docs/README-1.svg</c>, <c>docs/README-2.svg</c>. Names are predictable, so a
/// repeated export updates the same files and image links in documents stay valid.
/// </summary>
public static class DiagramExportPaths
{
    public static string For(string sourcePath, DiagramFormat format, int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var folder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var suffix = DiagramFiles.IsMarkdown(sourcePath) ? "-" + index.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return Path.Combine(folder, name + suffix + Extension(format));
    }

    public static string Extension(DiagramFormat format) => format == DiagramFormat.Png ? ".png" : ".svg";
}
