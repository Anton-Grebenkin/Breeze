using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Diagrams.Services.Rendering;

namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>
/// Exports diagrams to SVG or PNG next to the source (<see cref="DiagramExportPaths"/>) in the light theme: an image for
/// documents, READMEs and the model. A diagram with an error is skipped, the rest are written; each file is written
/// atomically. Used by the export commands and the agent tool.
/// </summary>
public sealed class DiagramExporter(IDiagramRenderer renderer, IFileSystem fileSystem, DiagramWrites writes)
{
    /// <exception cref="DiagramRendererException">The renderer is unavailable.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No write permission.</exception>
    public async Task<DiagramExport> ExportAsync(string sourcePath, IReadOnlyList<DiagramSource> diagrams, DiagramFormat format, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagrams);
        var written = new List<string>();
        var problems = new List<DiagramProblem>();
        foreach (var diagram in diagrams)
        {
            var result = await RenderAsync(diagram.Text, format, cancellationToken);
            if (!result.IsSuccess)
            {
                problems.Add(DiagramErrors.Locate(diagram, result.Error!));
                continue;
            }

            var path = DiagramExportPaths.For(sourcePath, format, diagram.Index);
            fileSystem.WriteAllBytesAtomic(path, result.Value);
            writes.Record(path);
            written.Add(path);
        }

        return new DiagramExport(written, problems);
    }

    private async Task<DiagramResult<byte[]>> RenderAsync(string text, DiagramFormat format, CancellationToken cancellationToken)
    {
        if (format == DiagramFormat.Png)
        {
            return await renderer.RenderPngAsync(text, DiagramTheme.Light, DiagramPngSize.Export, cancellationToken);
        }

        var svg = await renderer.RenderSvgAsync(text, DiagramTheme.Light, cancellationToken);
        return svg.IsSuccess ? DiagramResult<byte[]>.Success(Encoding.UTF8.GetBytes(svg.Value)) : DiagramResult<byte[]>.Failure(svg.Error!);
    }
}
