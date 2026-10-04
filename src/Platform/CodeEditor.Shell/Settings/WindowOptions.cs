using CodeEditor.Shell.Zoom;

namespace CodeEditor.Shell.Settings;

/// <summary>The <c>window</c> settings section, e.g. <c>"window.zoom": 110</c> for the interface zoom in percent.</summary>
public sealed class WindowOptions
{
    public const string Section = "window";

    public int Zoom { get; set; } = ZoomLevels.Default;
}
