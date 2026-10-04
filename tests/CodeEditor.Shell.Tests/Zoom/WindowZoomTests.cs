using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Settings;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Shell.Zoom;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Zoom;

public sealed class WindowZoomTests : IDisposable
{
    private const string SaveError = "Не удалось записать в настройки";

    private readonly ShellFixture _shell = new();
    private readonly TestOptionsMonitor<WindowOptions> _options = new(new WindowOptions());
    private readonly FakeSettingsService _settings = new();
    private readonly StatusBarViewModel _statusBar = new();
    private readonly WindowZoom _zoom;
    private readonly ZoomCommands _commands;

    public WindowZoomTests()
    {
        _zoom = new WindowZoom(_options, _settings, _statusBar);
        _commands = new ZoomCommands(_zoom);
        _commands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _zoom.Dispose();
        _shell.Dispose();
    }

    [Fact]
    public async Task ZoomIn_ScalesWindow_SavesLevel_AndShowsIt()
    {
        await Execute(ZoomCommands.ZoomInId);

        Assert.Equal((110, 1.1), (_zoom.Percent, _zoom.Scale));
        Assert.Equal(110, _settings.Written[WindowZoom.SettingKey]);
        Assert.Equal("Масштаб: " + 1.1.ToString("P0", CultureInfo.CurrentCulture), _statusBar.Message);
    }

    [Fact]
    public async Task Reset_RemovesSavedLevel()
    {
        await Execute(ZoomCommands.ZoomOutId);
        await Execute(ZoomCommands.ZoomResetId);

        Assert.Equal(ZoomLevels.Default, _zoom.Percent);
        Assert.Null(Assert.Contains(WindowZoom.SettingKey, _settings.Written));
    }

    [Fact]
    public void Level_FollowsSettings_Clamped()
    {
        var scales = new List<double>();
        _zoom.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WindowZoom.Scale))
            {
                scales.Add(_zoom.Scale);
            }
        };

        _options.Set(new WindowOptions { Zoom = 125 });
        _options.Set(new WindowOptions { Zoom = 1000 });

        Assert.Equal([1.25, 3.0], scales);
    }

    [Fact]
    public void Wheel_StepsByNotch()
    {
        _zoom.Wheel(WheelSteps.Notch / 2);
        var afterHalfNotch = _zoom.Percent;
        _zoom.Wheel(WheelSteps.Notch / 2);
        _zoom.Wheel(-3 * WheelSteps.Notch);

        Assert.Equal((100, 80), (afterHalfNotch, _zoom.Percent));
    }

    [Fact]
    public async Task BrokenSettings_ZoomStillChanges_AndErrorIsShown()
    {
        _settings.WriteError = SaveError;

        await Execute(ZoomCommands.ZoomInId);

        Assert.Equal(110, _zoom.Percent);
        Assert.Equal(SaveError, _statusBar.Message);
    }

    [Fact]
    public void Keys_MatchVsCode_WithKeypadVariants()
    {
        string? Resolve(string keys) =>
            _shell.Keybindings.GetByFirstChord(KeySequence.Parse(keys).First).LastOrDefault()?.CommandId;

        Assert.Equal(
            [ZoomCommands.ZoomInId, ZoomCommands.ZoomInId, ZoomCommands.ZoomInId, ZoomCommands.ZoomOutId, ZoomCommands.ZoomOutId, ZoomCommands.ZoomResetId, ZoomCommands.ZoomResetId],
            new[] { "Ctrl+=", "Ctrl+Shift+=", "Ctrl+Add", "Ctrl+-", "Ctrl+Subtract", "Ctrl+0", "Ctrl+NumPad0" }.Select(Resolve));
        Assert.Equal(KeySequence.Parse("Ctrl+="), _shell.Keybindings.FindForCommand(ZoomCommands.ZoomInId)?.Sequence);
    }

    [Fact]
    public void ViewMenu_HasZoomItems()
    {
        var items = _shell.MenuBuilder.Build(MenuIds.View);

        Assert.Equal(["_Увеличить", "У_меньшить", "Сбросить _масштаб"], items.Select(item => item.ToString()));
        Assert.Equal(["Ctrl+=", "Ctrl+-", "Ctrl+0"], items.Select(item => item.InputGestureText));
    }

    private async Task Execute(string commandId)
    {
        var status = await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionStatus.Succeeded, status);
    }
}
