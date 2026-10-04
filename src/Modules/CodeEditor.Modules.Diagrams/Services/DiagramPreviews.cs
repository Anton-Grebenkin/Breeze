using System.Globalization;
using CodeEditor.Modules.Diagrams.Resources;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Diagrams.Services;

/// <summary>
/// Diagram preview tabs (ADR 0035): an editor view with id <c>diagram.preview:&lt;path&gt;</c> beside the text
/// (<see cref="EditorViewRequest.ToTheSide"/>). Repeating the command for the same file activates the open tab.
/// </summary>
public sealed class DiagramPreviews(IEditorViews views, DiagramPreviewServices services)
{
    public const string IdPrefix = "diagram.preview:";

    public static string IdFor(string filePath) => IdPrefix + Path.GetFullPath(filePath);

    /// <summary>Opens the file preview or activates the open one. Called on the UI thread.</summary>
    public EditorTab Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var title = string.Format(CultureInfo.CurrentCulture, Strings.PreviewTitle, Path.GetFileName(filePath));
        return views.Open(new EditorViewRequest(IdFor(filePath), title, () => new DiagramPreviewViewModel(filePath, services))
        {
            ToolTip = filePath,
            ToTheSide = true,
        });
    }
}
