using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// SVG viewer in a WebView2 page (ADR 0037): the drawing is an <c>&lt;img&gt;</c> element, so scripts and links inside
/// the SVG do not run. Zoom is the same as for images (<see cref="ZoomState"/>), but the page computes fit and wheel
/// zoom and reports them here. SVG is text: an agent edit and "Open as text" open it in the text editor.
/// </summary>
public sealed partial class SvgViewerViewModel(string filePath, ViewerContext context)
    : WebViewerViewModel(filePath, ViewerKind.Svg, context), IZoomableViewer
{
    private const string FormatName = "SVG";

    public ZoomState Zoom { get; } = new();

    /// <summary>Drawing size in CSS pixels (from width, height or viewBox); <c>null</c> until the page renders it.</summary>
    [ObservableProperty]
    public partial PixelSize? NaturalSize { get; private set; }

    public override void Receive(ViewerPageMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        switch (message.Type)
        {
            case ViewerPageMessages.Size when message is { Width: { } width and > 0, Height: { } height and > 0 }:
                NaturalSize = new PixelSize((int)Math.Ceiling(width), (int)Math.Ceiling(height));
                ShowError(null);
                UpdateSummary();
                break;
            case ViewerPageMessages.Zoom when message.Value is { } value:
                Zoom.Report(value, message.Fit);
                break;
            case ViewerPageMessages.Error:
                ShowError(Strings.SvgInvalid);
                break;
            default:
                break;
        }
    }

    protected override void UpdateSummary() =>
        Summary = NaturalSize is { } size
            ? Format(Strings.ImageSummary, size.Width, size.Height, FormatName, ByteSizes.Format(FileLength))
            : Format(Strings.FileSummary, FormatName, ByteSizes.Format(FileLength));
}
