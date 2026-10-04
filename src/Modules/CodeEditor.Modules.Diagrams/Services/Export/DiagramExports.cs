using System.Globalization;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.Services.Rendering;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Diagrams.Services.Export;

/// <summary>
/// The "Export as SVG" and "Export as PNG" commands: all diagrams of the file go next to the source
/// (<see cref="DiagramExporter"/>), and the status bar shows the result: "Saved: arch.svg." or an error with its line.
/// Files are overwritten without asking: the user requested the export, and the names are predictable
/// (<see cref="DiagramExportPaths"/>).
/// </summary>
public sealed class DiagramExports(DiagramExporter exporter, StatusBarViewModel statusBar)
{
    /// <param name="text">The file text: the document with unsaved edits or the file from disk.</param>
    public async Task ExportAsync(string path, Func<Task<string>> text, DiagramFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var diagrams = DiagramFiles.Extract(path, await text());
            statusBar.Message = diagrams.Count == 0
                ? Strings.ExportNothing
                : Summary(await exporter.ExportAsync(path, diagrams, format, CancellationToken.None));
        }
        catch (DiagramRendererException exception)
        {
            statusBar.Message = exception.Message;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = Format(Strings.ExportFailed, exception.Message);
        }
    }

    /// <summary>"Saved: README-1.svg, README-2.svg." plus the first diagram that was not saved.</summary>
    public static string Summary(DiagramExport export)
    {
        ArgumentNullException.ThrowIfNull(export);
        var saved = export.Written.Count == 0 ? null : Format(Strings.ExportSaved, string.Join(", ", export.Written.Select(Path.GetFileName)));
        var failed = export.Problems.Count == 0 ? null : Format(Strings.ExportNotSaved, DiagramErrors.Brief(export.Problems[0]));
        return string.Join(' ', new[] { saved, failed }.OfType<string>());
    }

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
