using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.ViewModels;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Modules.Search.Commands;

/// <summary>
/// Search commands: case, whole-word and regex toggles (<c>Alt+C</c>, <c>Alt+W</c>, <c>Alt+R</c> in the panel,
/// as in VS Code), clearing results and "Edit → Find in Files".
/// </summary>
public sealed class SearchCommands(Func<SearchViewModel> search) : IDisposable
{
    public const string ToggleCaseId = "search.toggleCaseSensitive";
    public const string ToggleWholeWordId = "search.toggleWholeWord";
    public const string ToggleRegexId = "search.toggleRegex";
    public const string ClearId = "search.clear";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        Add(commands, ToggleCaseId, Strings.MatchCase, vm => vm.MatchCase = !vm.MatchCase);
        Add(commands, ToggleWholeWordId, Strings.WholeWord, vm => vm.WholeWord = !vm.WholeWord);
        Add(commands, ToggleRegexId, Strings.UseRegex, vm => vm.UseRegex = !vm.UseRegex);
        Add(commands, ClearId, Strings.ClearSearchResults, vm => vm.Clear());

        var searchFocus = ContextExpression.Parse(SearchViewModel.FocusContextKey);
        Bind(keybindings, "Alt+C", ToggleCaseId, searchFocus);
        Bind(keybindings, "Alt+W", ToggleWholeWordId, searchFocus);
        Bind(keybindings, "Alt+R", ToggleRegexId, searchFocus);

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Edit, ToolWindowAreaViewModel.ShowCommandId(SearchModule.ToolWindowId), "4_find", order: 10, title: Strings.MenuFindInFiles)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void Add(ICommandRegistry commands, string id, string title, Action<SearchViewModel> handler) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            handler(search());
            return ValueTask.CompletedTask;
        }, Strings.ModuleName)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));
}
