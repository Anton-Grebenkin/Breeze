using CodeEditor.Core.Commands;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Tests.Infrastructure;

namespace CodeEditor.Shell.Tests.Palette;

public sealed class CommandPaletteViewModelTests
{
    private readonly ShellFixture _shell = new();
    private readonly RecentCommands _recent = new();
    private readonly List<string> _executed = [];
    private readonly StubProvider _files = new();
    private readonly CommandPaletteViewModel _palette;

    public CommandPaletteViewModelTests()
    {
        var commands = new CommandsQuickOpenProvider(_shell.Commands, _shell.Keybindings, _shell.CommandService, _shell.Context, _recent);
        _palette = new CommandPaletteViewModel([_files, commands], _shell.Context);

        Register("theme.dark", "Тёмная тема");
        Register("theme.light", "Светлая тема");
        Register("file.save", "Сохранить", when: "editorFocus");
        Register(ShellCommandIds.ShowCommands, "Показать все команды");
        _shell.Bind("Ctrl+K Ctrl+L", "theme.light");
    }

    [Fact]
    public void Open_ListsAvailableCommandsExceptItself()
    {
        _palette.Open();

        Assert.True(_palette.IsOpen);
        Assert.Equal(["Светлая тема", "Тёмная тема"], _palette.Items.Select(item => item.Title));
        Assert.Equal(0, _palette.SelectedIndex);
        Assert.Equal(true, _shell.Context.GetValue(CommandPaletteViewModel.VisibleContextKey));
    }

    [Fact]
    public void Open_ShowsShortcuts()
    {
        _palette.Open();

        Assert.Equal("Ctrl+K Ctrl+L", _palette.Items.Single(item => item.Id == "theme.light").Shortcut);
    }

    [Fact]
    public void Query_FiltersAndResetsSelection()
    {
        _palette.Open();
        _palette.MoveNextCommand.Execute(null);

        _palette.Query = ">тём";

        Assert.Equal(["theme.dark"], _palette.Items.Select(item => item.Id));
        Assert.Equal(0, _palette.SelectedIndex);
    }

    [Fact]
    public void Query_WithoutMatches_IsEmpty()
    {
        _palette.Open();

        _palette.Query = ">zzz";

        Assert.True(_palette.IsEmpty);
        Assert.Equal(-1, _palette.SelectedIndex);
    }

    [Fact]
    public void MoveNextAndPrevious_Wrap()
    {
        _palette.Open();

        _palette.MovePreviousCommand.Execute(null);
        Assert.Equal(1, _palette.SelectedIndex);

        _palette.MoveNextCommand.Execute(null);
        Assert.Equal(0, _palette.SelectedIndex);
    }

    [Fact]
    public async Task Accept_ExecutesSelectedClosesAndRemembers()
    {
        _palette.Open();
        _palette.Query = ">тём";

        await _palette.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(["theme.dark"], _executed);
        Assert.False(_palette.IsOpen);
        Assert.Equal(false, _shell.Context.GetValue(CommandPaletteViewModel.VisibleContextKey));
        Assert.Equal(["theme.dark"], _recent.Items);
    }

    [Fact]
    public async Task RecentCommand_IsListedFirstNextTime()
    {
        _palette.Open();
        await _palette.ExecuteItemCommand.ExecuteAsync(_palette.Items.Single(item => item.Id == "theme.dark"));

        _palette.Open();

        Assert.Equal("theme.dark", _palette.Items[0].Id);
        Assert.True(_palette.Items[0].IsRecent);
    }

    [Fact]
    public void Close_ClearsItems()
    {
        _palette.Open();

        _palette.CloseCommand.Execute(null);

        Assert.False(_palette.IsOpen);
        Assert.Empty(_palette.Items);
    }

    [Fact]
    public void Open_ReevaluatesWhenConditions()
    {
        _shell.Context.Set("editorFocus", true);

        _palette.Open();

        Assert.Contains(_palette.Items, item => item.Id == "file.save");
    }

    [Fact]
    public void Query_SwitchesModeByPrefix()
    {
        _palette.Open(string.Empty);

        Assert.Equal(StubProvider.Title, _palette.Items.Single().Title);
        Assert.Equal(_files.Placeholder, _palette.Placeholder);

        _palette.Query = ">";

        Assert.Equal(["Светлая тема", "Тёмная тема"], _palette.Items.Select(item => item.Title));
        Assert.Equal("Введите название команды", _palette.Placeholder);
        Assert.Equal("Нет подходящих команд", _palette.EmptyText);
    }

    [Fact]
    public async Task Accept_PassesTextWithoutPrefixToMode()
    {
        _palette.Open(string.Empty);
        _palette.Query = "abc";

        await _palette.AcceptCommand.ExecuteAsync(null);

        Assert.Equal("abc", _files.AcceptedText);
        Assert.False(_palette.IsOpen);
    }

    [Fact]
    public void Open_PreparesModeEachTime()
    {
        _palette.Open(string.Empty);
        _palette.Close();
        _palette.Open(string.Empty);

        Assert.Equal(2, _files.PrepareCount);
    }

    [Fact]
    public void ItemsChanged_WhileOpen_RefreshesCurrentMode()
    {
        _palette.Open(string.Empty);

        _files.RaiseItemsChanged();

        Assert.Equal(2, _files.PrepareCount);
        Assert.Single(_palette.Items);
    }

    [Fact]
    public void ItemsChanged_AfterClose_IsIgnored()
    {
        _palette.Open(string.Empty);
        _palette.Close();

        _files.RaiseItemsChanged();

        Assert.False(_files.HasSubscribers);
        Assert.Empty(_palette.Items);
    }

    private void Register(string id, string title, string? when = null) =>
        _shell.Commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            _executed.Add(id);
            return ValueTask.CompletedTask;
        }, when: when is null ? null : Core.Context.ContextExpression.Parse(when)));
}
