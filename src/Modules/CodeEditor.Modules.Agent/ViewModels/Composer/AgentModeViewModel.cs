using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>
/// The agent mode next to the input box: agent, ask or plan. Picked in the palette and stored in the user's
/// <c>settings.json</c> (<c>agent.mode</c>).
/// </summary>
public sealed partial class AgentModeViewModel : ObservableObject, IDisposable
{
    public const string ModeKey = "agent.mode";

    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly ISettingsService _settings;
    private readonly IQuickPick _quickPick;
    private readonly StatusBarViewModel _statusBar;
    private readonly IDisposable? _subscription;

    public AgentModeViewModel(IOptionsMonitor<AgentOptions> options, ISettingsService settings, IQuickPick quickPick, StatusBarViewModel statusBar)
    {
        _options = options;
        _settings = settings;
        _quickPick = quickPick;
        _statusBar = statusBar;
        _subscription = options.OnChange(_ => OnPropertyChanged(string.Empty));
    }

    public AgentMode Mode => _options.CurrentValue.Mode;

    public string Title => AgentModes.Title(Mode);

    public string ToolTip => string.Format(CultureInfo.CurrentCulture, Strings.ModeToolTip, Title.ToLowerInvariant(), AgentModes.Description(Mode));

    public void Dispose() => _subscription?.Dispose();

    /// <returns><c>false</c> if the setting was not saved; the error goes to the status bar.</returns>
    public bool SetMode(AgentMode mode)
    {
        if (!_settings.TrySetUserValue(ModeKey, AgentModes.SettingValue(mode), out var error))
        {
            _statusBar.Message = error;
            return false;
        }

        _statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.ModeChanged, AgentModes.Title(mode).ToLowerInvariant());
        return true;
    }

    [RelayCommand]
    private void PickMode()
    {
        var items = AgentModes.Selectable
            .Select(mode => new QuickPickItem(mode.ToString(), AgentModes.Title(mode))
            {
                Detail = mode == Mode
                    ? string.Format(CultureInfo.CurrentCulture, Strings.CurrentModeDetail, AgentModes.Description(mode))
                    : AgentModes.Description(mode),
            })
            .ToList();
        _quickPick.Show(new QuickPickProvider(Strings.ModePickTitle, items, item =>
        {
            SetMode(Enum.Parse<AgentMode>(item.Id));
            return Task.CompletedTask;
        }));
    }
}
