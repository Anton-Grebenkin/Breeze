using System.Windows;

namespace CodeEditor.Modules.Viewers.Wpf.Views;

/// <summary>
/// The toolbar above a viewer: file info, loading, note, error, Refresh, Open in external app and, for SVG, Open as
/// text. Viewer-specific actions (zoom, go to offset) go into <see cref="Tools"/>.
/// </summary>
public sealed partial class ViewerToolbar
{
    public ViewerToolbar() => InitializeComponent();

    /// <summary>Viewer-specific actions, left of the common ones; they get the same view model.</summary>
    public UIElement? Tools
    {
        get => ToolsHost.Child;
        set => ToolsHost.Child = value;
    }
}
