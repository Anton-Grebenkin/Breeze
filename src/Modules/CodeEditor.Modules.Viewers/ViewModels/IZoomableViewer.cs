namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>A viewer with zoom (image and SVG): the zoom commands and their keys apply to it.</summary>
public interface IZoomableViewer
{
    ZoomState Zoom { get; }
}
