using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Zoom;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// Editor font zoom: Ctrl + wheel over the editor (as in Visual Studio and VS Code with <c>editor.mouseWheelZoom</c>)
/// and the Editor Font Zoom commands. A step is one point. The zoom is saved to <c>editor.fontZoom</c> in user
/// settings, so all editors follow it and it survives a restart; the status bar shows the new size.
/// </summary>
public sealed class EditorFontZoom(EditorSettings editor, ISettingsService settings, StatusBarViewModel statusBar)
{
    public const string SettingKey = "editor.fontZoom";

    private readonly WheelSteps _wheel = new();

    public void ZoomIn() => ZoomBy(1);

    public void ZoomOut() => ZoomBy(-1);

    public void Reset() => Apply(0);

    /// <summary>Ctrl + wheel: a notch is one step, touchpad deltas add up.</summary>
    public void Wheel(int delta)
    {
        var steps = _wheel.Add(delta);
        if (steps != 0)
        {
            ZoomBy(steps);
        }
    }

    private void ZoomBy(int steps) => Apply(editor.FontZoom + (steps * EditorSettings.ZoomStep));

    // The size applies immediately; if settings.json is broken, it isn't saved and the status bar says why.
    // No zoom removes the key, so the file keeps only what the user changed.
    private void Apply(double zoom)
    {
        var before = editor.FontZoom;
        editor.SetFontZoom(zoom);

        string? error = null;
        var zoomed = editor.FontZoom;
        var saved = zoomed.Equals(before)
            || settings.TrySetUserValue(SettingKey, zoomed == 0 ? null : zoomed, out error);
        statusBar.Message = saved
            ? string.Format(CultureInfo.CurrentCulture, Strings.EditorFontSize, editor.FontSize)
            : error!;
    }
}
