using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Settings;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class WindowsIntegrationCommandsTests : IDisposable
{
    private readonly ShellFixture _shell = new();
    private readonly FakeSettingsService _settings = new();
    private readonly TestOptionsMonitor<WindowsIntegrationOptions> _options = new(new WindowsIntegrationOptions());
    private readonly FakeQuickPick _quickPick = new();
    private readonly StatusBarViewModel _statusBar = new();

    [Theory]
    [InlineData(WindowsIntegrationCommands.EnableFileTypesId, true)]
    [InlineData(WindowsIntegrationCommands.DisableFileTypesId, false)]
    public async Task FileTypeCommands_SaveTheAnswer(string command, bool expected)
    {
        using var commands = Register(installed: true);

        await _shell.CommandService.ExecuteAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, _settings.Written[WindowsIntegrationOptions.FileTypesKey]);
        Assert.False(string.IsNullOrEmpty(_statusBar.Message));
    }

    [Fact]
    public async Task Configure_TogglesTheChosenFeature()
    {
        _options.Set(new WindowsIntegrationOptions { ContextMenu = true, FileTypes = null });
        using var commands = Register(installed: true);

        await _shell.CommandService.ExecuteAsync(WindowsIntegrationCommands.ConfigureId, cancellationToken: TestContext.Current.CancellationToken);
        await _quickPick.Shown!.AcceptAsync(_quickPick.Items[1], string.Empty);
        await _quickPick.Shown.AcceptAsync(_quickPick.Items[0], string.Empty);

        Assert.Equal(false, _settings.Written[WindowsIntegrationOptions.ContextMenuKey]);
        Assert.Equal(true, _settings.Written[WindowsIntegrationOptions.FileTypesKey]);
    }

    [Fact]
    public void Configure_IsInTheManageMenu()
    {
        using var commands = Register(installed: true);

        Assert.Contains(_shell.Menus.GetItems(MenuIds.Manage), item => item.CommandId == WindowsIntegrationCommands.ConfigureId);
    }

    // A portable or development build registers nothing in Windows, so it offers no commands for it.
    [Fact]
    public void NotInstalled_RegistersNoCommands()
    {
        using var commands = Register(installed: false);

        Assert.False(_shell.CommandService.CanExecute(WindowsIntegrationCommands.ConfigureId));
        Assert.False(_shell.CommandService.CanExecute(WindowsIntegrationCommands.EnableFileTypesId));
    }

    [Fact]
    public async Task FailedWrite_IsReported()
    {
        _settings.WriteError = "settings.json: доступ запрещён";
        using var commands = Register(installed: true);

        await _shell.CommandService.ExecuteAsync(WindowsIntegrationCommands.EnableFileTypesId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("settings.json: доступ запрещён", _statusBar.Message);
    }

    public void Dispose() => _shell.Dispose();

    private WindowsIntegrationCommands Register(bool installed)
    {
        var commands = new WindowsIntegrationCommands(new FakeIntegration(installed), _settings, _options, _quickPick, _statusBar);
        commands.Register(_shell.Commands, _shell.Menus);
        return commands;
    }

    private sealed class FakeIntegration(bool installed) : IWindowsIntegration
    {
        public bool IsAvailable => installed;
    }
}
