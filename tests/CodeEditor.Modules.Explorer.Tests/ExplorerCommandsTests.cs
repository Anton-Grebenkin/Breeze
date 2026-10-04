using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Explorer.Commands;
using CodeEditor.Shell.Commands;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerCommandsTests : IDisposable
{
    private readonly ExplorerFixture _fixture = new();
    private readonly KeybindingRegistry _keybindings = new();
    private readonly MenuRegistry _menus = new();
    private readonly ExplorerCommands _commands;

    public ExplorerCommandsTests()
    {
        _commands = new ExplorerCommands(_fixture.Explorer, _fixture.Editor, _fixture.CommandService);
        _commands.Register(_fixture.Commands, _keybindings, _menus);
    }

    public void Dispose()
    {
        _commands.Dispose();
        _fixture.Dispose();
    }

    [Theory]
    [InlineData("F2", ExplorerCommands.RenameId)]
    [InlineData("Delete", ExplorerCommands.DeleteId)]
    [InlineData("Enter", ExplorerCommands.OpenId)]
    public void TreeKeys_WorkOnlyWhenTreeFocusedAndNotEditing(string keys, string commandId)
    {
        var resolver = new KeybindingResolver(_keybindings);
        var chord = KeySequence.Parse(keys).First;

        Assert.Equal(KeyResolutionKind.NotHandled, resolver.Resolve(chord, _fixture.Context).Kind);

        _fixture.Context.Set("explorerFocus", true);
        Assert.Equal(commandId, resolver.Resolve(chord, _fixture.Context).Binding?.CommandId);

        _fixture.Context.Set("explorerEditing", true);
        Assert.Equal(KeyResolutionKind.NotHandled, resolver.Resolve(chord, _fixture.Context).Kind);
    }

    [Fact]
    public async Task Commands_AreAvailableOnlyWithOpenFolder()
    {
        Assert.False(_fixture.CommandService.CanExecute(ExplorerCommands.NewFileId));

        await _fixture.OpenAsync();

        Assert.True(_fixture.CommandService.CanExecute(ExplorerCommands.NewFileId));
    }

    [Fact]
    public async Task DeleteCommand_DeletesSelectedNode()
    {
        await _fixture.OpenAsync();
        _fixture.Node("README.md").IsSelected = true;

        var status = await _fixture.CommandService.ExecuteAsync(ExplorerCommands.DeleteId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CommandExecutionStatus.Succeeded, status);
        Assert.Single(_fixture.FileSystem.RecycledPaths);
    }

    [Fact]
    public void ContextMenu_GroupsActionsLikeVsCode()
    {
        var groups = _menus.GetItems(ExplorerCommands.ContextMenuId).GroupBy(item => item.Group).Select(group => group.Count());

        Assert.Equal([2, 1, 2, 2, 2], groups);
    }

    [Fact]
    public async Task OpenInTerminal_StartsATerminalInTheSelectedFolder()
    {
        string? folder = null;
        _fixture.Commands.Register(new CommandDefinition(ShellCommandIds.NewTerminal, "Новый терминал", (argument, _) =>
        {
            folder = argument as string;
            return ValueTask.CompletedTask;
        }));
        await _fixture.OpenAsync();
        _fixture.Node("docs").IsSelected = true;

        await _fixture.CommandService.ExecuteAsync(ExplorerCommands.OpenInTerminalId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(ExplorerFixture.Root, "docs"), folder);
    }
}
