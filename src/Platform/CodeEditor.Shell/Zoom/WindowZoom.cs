using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Settings;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;

namespace CodeEditor.Shell.Zoom;

/// <summary>
/// Interface zoom, like <c>window.zoomLevel</c> in VS Code: the whole window scales and text stays sharp (the view
/// applies <see cref="Scale"/>). The level is saved to <c>window.zoom</c> in user settings, follows manual edits of the
/// file and is shown in the status bar after each change.
/// </summary>
public sealed partial class WindowZoom : ObservableObject, IDisposable
{
    public const string SettingKey = "window.zoom";

    private const double PercentPerUnit = 100;

    private readonly ISettingsService _settings;
    private readonly StatusBarViewModel _statusBar;
    private readonly WheelSteps _wheel = new();
    private readonly IDisposable? _subscription;

    public WindowZoom(IOptionsMonitor<WindowOptions> options, ISettingsService settings, StatusBarViewModel statusBar)
    {
        _settings = settings;
        _statusBar = statusBar;
        Percent = ZoomLevels.Clamp(options.CurrentValue.Zoom);
        _subscription = options.OnChange(changed => Percent = ZoomLevels.Clamp(changed.Zoom));
    }

    /// <summary>The zoom in percent, <see cref="ZoomLevels.Default"/> without zoom.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scale))]
    public partial int Percent { get; private set; }

    /// <summary>The layout scale for the window content: 1.1 for 110 %.</summary>
    public double Scale => Percent / PercentPerUnit;

    public void ZoomIn() => ZoomBy(1);

    public void ZoomOut() => ZoomBy(-1);

    public void Reset() => Apply(ZoomLevels.Default);

    /// <summary>Ctrl + wheel: a notch is one step, touchpad deltas add up.</summary>
    public void Wheel(int delta)
    {
        var steps = _wheel.Add(delta);
        if (steps != 0)
        {
            ZoomBy(steps);
        }
    }

    public void Dispose() => _subscription?.Dispose();

    private void ZoomBy(int steps) => Apply(ZoomLevels.Move(Percent, steps));

    // The zoom applies immediately; if settings.json is broken, it isn't saved and the status bar says why.
    // The default level removes the key, so the file keeps only what the user changed.
    private void Apply(int percent)
    {
        string? error = null;
        var saved = percent == Percent
            || _settings.TrySetUserValue(SettingKey, percent == ZoomLevels.Default ? null : percent, out error);
        Percent = percent;
        _statusBar.Message = saved
            ? string.Format(CultureInfo.CurrentCulture, Strings.ZoomLevel, Scale.ToString("P0", CultureInfo.CurrentCulture))
            : error!;
    }
}
