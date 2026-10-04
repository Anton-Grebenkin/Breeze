using CodeEditor.Core.Commands;
using CodeEditor.Core.Storage;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Commands;

public sealed class LayoutCommandsTests : IDisposable
{
    private readonly LayoutFixture _fixture;
    private readonly ToolWindowRegistry _toolWindows;
    private readonly ShellFixture _shell;
    private readonly WorkbenchLayout _layout;
    private readonly FakeQuickPick _quickPick = new();
    private readonly LayoutCommands _commands;

    public LayoutCommandsTests()
    {
        var store = new JsonLayoutStore(new UserDataPaths(Path.GetTempPath()), NullLogger<JsonLayoutStore>.Instance);
        _fixture = new LayoutFixture(store);
        (_toolWindows, _shell, _layout) = (_fixture.Registry, _fixture.Shell, _fixture.Layout);
        _commands = new LayoutCommands(_layout, _fixture.EditorWindows, _quickPick);
        _commands.Register(_shell.Commands, _shell.Keybindings, _shell.Menus, _toolWindows);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task ToolWindow_GetsShowCommandKeybindingAndMenuItem()
    {
        _toolWindows.Register(new ToolWindowDefinition("output", "Вывод", "", ToolWindowLocation.Panel, () => new object())
        {
            Keybinding = "Ctrl+Shift+U",
        });
        var commandId = ToolWindowAreaViewModel.ShowCommandId("output");

        await Execute(commandId);

        Assert.True(_layout.Panel.IsVisible);
        Assert.Equal("Ctrl+Shift+U", _shell.Keybindings.FindForCommand(commandId)?.Sequence.ToString());
        Assert.Contains(_shell.MenuBuilder.Build(MenuIds.View), item => item.Header == "Вывод");
    }

    [Fact]
    public void UnregisteredToolWindow_LosesItsCommand()
    {
        var registration = _toolWindows.Register(new ToolWindowDefinition("output", "Вывод", "", ToolWindowLocation.Panel, () => new object()));

        registration.Dispose();

        Assert.False(_shell.Commands.TryGet(ToolWindowAreaViewModel.ShowCommandId("output"), out _));
    }

    [Fact]
    public async Task TogglePanel_ShowsAndHides()
    {
        _toolWindows.Register(new ToolWindowDefinition("output", "Вывод", "", ToolWindowLocation.Panel, () => new object()));

        await Execute(LayoutCommands.TogglePanelId);
        Assert.True(_layout.Panel.IsVisible);

        await Execute(LayoutCommands.TogglePanelId);
        Assert.False(_layout.Panel.IsVisible);
    }

    // A hidden panel shows maximized; toggling again restores the size; hiding resets it.
    [Fact]
    public async Task ToggleMaximizedPanel_ShowsMaximized_RestoresAndResetsOnHide()
    {
        _toolWindows.Register(new ToolWindowDefinition("output", "Вывод", "", ToolWindowLocation.Panel, () => new object()));

        await Execute(LayoutCommands.ToggleMaximizedPanelId);
        Assert.Equal((true, true), (_layout.Panel.IsVisible, _layout.Panel.IsMaximized));

        await Execute(LayoutCommands.ToggleMaximizedPanelId);
        Assert.Equal((true, false), (_layout.Panel.IsVisible, _layout.Panel.IsMaximized));

        await Execute(LayoutCommands.ToggleMaximizedPanelId);
        await Execute(LayoutCommands.TogglePanelId);
        Assert.Equal((false, false), (_layout.Panel.IsVisible, _layout.Panel.IsMaximized));
        Assert.False(_layout.SideBar.CanMaximize);
    }

    [Theory]
    [InlineData(LayoutCommands.ToggleSideBarId, "Ctrl+B")]
    [InlineData(LayoutCommands.TogglePanelId, "Ctrl+J")]
    public void Toggles_HaveVsCodeShortcuts(string commandId, string keys)
    {
        Assert.Equal(keys, _shell.Keybindings.FindForCommand(commandId)?.Sequence.ToString());
    }

    private async Task Execute(string commandId)
    {
        var status = await _shell.CommandService.ExecuteAsync(commandId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(CommandExecutionStatus.Succeeded, status);
    }
}
