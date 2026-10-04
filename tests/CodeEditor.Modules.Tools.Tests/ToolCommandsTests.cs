using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Tools.Commands;
using CodeEditor.Modules.Tools.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Tools.Tests;

/// <summary>The Tools menu and the palette: a command per tool, parameters in the palette, a question for new code.</summary>
public sealed class ToolCommandsTests : IDisposable
{
    private readonly ToolsFixture _fixture = new();
    private readonly CommandRegistry _registry = new();
    private readonly MenuRegistry _menus = new();
    private readonly FakeQuickPick _quickPick = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly CommandService _commands;
    private readonly ToolCommands _toolCommands;

    public ToolCommandsTests()
    {
        _commands = new CommandService(_registry, new ContextKeyService(), NullLogger<CommandService>.Instance);
        _fixture.AddTool("count", "---\ndescription: Counts lines\nparameters:\n  path: Folder\n  pattern: Mask\n---");
        _toolCommands = new ToolCommands(_fixture.Shelf, _fixture.Runner, _fixture.Trust, _fixture.Activity, new ToolParameterPrompt(_quickPick),
            new ToolScaffold(_fixture.Workspace, _fixture.FileSystem), _dialogs, _quickPick, _commands, new InlineUiDispatcher(), _fixture.StatusBar);
        _toolCommands.Register(_registry, _menus);
    }

    public void Dispose()
    {
        _toolCommands.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void EveryTool_HasCommandAndMenuItem()
    {
        Assert.True(_registry.TryGet(ToolCommandIds.Run("count"), out var command));
        Assert.Equal("Инструменты: count", command.DisplayTitle);
        Assert.Contains(_menus.GetItems(ToolCommands.MenuId), item => item.CommandId == ToolCommandIds.Run("count"));
        Assert.Contains(_menus.GetItems(ToolCommands.MenuId), item => item.CommandId == ToolCommandIds.NewTool);
    }

    [Fact]
    public async Task Run_AsksParametersInPalette_ConfirmsNewCode_ThenRuns()
    {
        await ExecuteAsync(ToolCommandIds.Run("count"));
        await _quickPick.PickAsync("src");
        await _quickPick.PickAsync(string.Empty);

        Assert.Equal(["Запустить инструмент «count»?"], _dialogs.Confirmations);
        var request = Assert.Single(_fixture.Processes.Requests);
        Assert.Equal(["-path", "src"], request.Arguments.TakeLast(2));
        Assert.False(request.HideSecretVariables);

        await ExecuteAsync(ToolCommandIds.Run("count"), new Dictionary<string, string> { ["path"] = "docs" });

        Assert.Single(_dialogs.Confirmations);
        Assert.Equal(2, _fixture.Processes.Requests.Count);
    }

    [Fact]
    public async Task DeclinedConfirmation_DoesNotRun()
    {
        _dialogs.ConfirmAnswer = false;

        await ExecuteAsync(ToolCommandIds.Run("count"), new Dictionary<string, string>());

        Assert.Empty(_fixture.Processes.Requests);
        Assert.False(_fixture.Trust.IsTrusted(_fixture.Shelf.Find("count")!));
    }

    [Fact]
    public async Task NewTool_CreatesFromTemplate_AndAppearsInMenu()
    {
        await ExecuteAsync(ToolCommandIds.NewTool);
        await _quickPick.PickAsync("lint");

        var tool = _fixture.Shelf.Find("lint");
        Assert.NotNull(tool);
        Assert.Equal(("Считает строки в файлах папки", "path, pattern", ToolScriptKind.PowerShell), (tool.Description, tool.Signature, tool.Kind));
        Assert.Empty(_fixture.Shelf.Problems);
        Assert.True(_registry.TryGet(ToolCommandIds.Run("lint"), out _));
    }

    private async Task ExecuteAsync(string id, object? argument = null) =>
        Assert.Equal(CommandExecutionStatus.Succeeded, await _commands.ExecuteAsync(id, argument, TestContext.Current.CancellationToken));

    [Fact]
    public async Task NewTool_RejectsBadNames()
    {
        await ExecuteAsync(ToolCommandIds.NewTool);

        Assert.Empty(_quickPick.Shown!.Filter("two words"));
        Assert.Single(_quickPick.Shown!.Filter("two-words"));
    }
}
