using CodeEditor.Core.Documents;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// File viewers for editor tabs (ADR 0031, 0037): images, SVG, audio, video and known binary formats open as something
/// other than text. A file that fails to open as text (binary or too large) is shown in the hex view instead of an
/// error. Creating a view model reads nothing; the first show does.
/// </summary>
public sealed class ViewerProvider(ViewerContext context) : IFileViewerProvider
{
    /// <summary>Below document viewers (10): the generic viewer yields to a specific one on shared extensions.</summary>
    public int Priority => 5;

    public bool CanOpen(string filePath) => ViewerKinds.Of(filePath) != ViewerKind.None;

    /// <summary>SVG is text: an agent edit opens it in the text editor instead of the drawing.</summary>
    public bool IsText(string filePath) => ViewerKinds.Of(filePath) == ViewerKind.Svg;

    public bool OpensInsteadOfText(string filePath, DocumentOpenFailure failure) =>
        failure is DocumentOpenFailure.Binary or DocumentOpenFailure.TooLarge;

    public object CreateViewer(string filePath) => ViewerKinds.Of(filePath) switch
    {
        ViewerKind.Image => new ImageViewerViewModel(filePath, context),
        ViewerKind.Svg => new SvgViewerViewModel(filePath, context),
        ViewerKind.Audio => new MediaViewerViewModel(filePath, ViewerKind.Audio, context),
        ViewerKind.Video => new MediaViewerViewModel(filePath, ViewerKind.Video, context),
        _ => new HexViewerViewModel(filePath, context),
    };
}
