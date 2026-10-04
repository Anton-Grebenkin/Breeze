using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Shell.Menus;

/// <summary>
/// Builds the <see cref="MenuItemViewModel"/> tree from the menu registry. Items of unregistered commands and empty
/// submenus are skipped; groups are divided by separators that show only between visible items
/// (<see cref="MenuSeparators"/>).
/// </summary>
public sealed class MenuBuilder(
    IMenuRegistry menus,
    ICommandRegistry commands,
    IKeybindingRegistry keybindings,
    ICommandService commandService,
    IContextKeyService context)
{
    /// <summary>Guards against submenu cycles declared by modules.</summary>
    private const int MaxDepth = 8;

    public IReadOnlyList<MenuItemViewModel> Build(string menuId) => Build(menuId, depth: 0);

    private List<MenuItemViewModel> Build(string menuId, int depth)
    {
        var result = new List<MenuItemViewModel>();
        if (depth > MaxDepth)
        {
            return result;
        }

        string? currentGroup = null;
        foreach (var definition in menus.GetItems(menuId))
        {
            var item = definition.IsSubmenu ? CreateSubmenu(definition, depth) : CreateCommand(definition);
            if (item is null)
            {
                continue;
            }

            if (currentGroup is not null && definition.Group != currentGroup)
            {
                result.Add(MenuItemViewModel.CreateSeparator());
            }

            currentGroup = definition.Group;
            item.RefreshState();
            result.Add(item);
        }

        MenuSeparators.Update(result);
        return result;
    }

    private MenuItemViewModel? CreateCommand(MenuItemDefinition definition)
    {
        if (!commands.TryGet(definition.CommandId!, out var command))
        {
            return null;
        }

        var commandId = command.Id;
        var argument = definition.Argument;
        var relay = new AsyncRelayCommand(
            async () => await commandService.ExecuteAsync(commandId, argument),
            () => commandService.CanExecute(commandId));

        // Items of one command with different arguments ("Open Recent") are told apart by their order.
        var automationKey = argument is null ? commandId : $"{commandId}.{definition.Order}";

        return MenuItemViewModel.ForCommand(
            definition.Title ?? command.Title,
            automationKey,
            keybindings.FindForCommand(commandId)?.Sequence.ToString(),
            relay,
            definition.When,
            context);
    }

    private MenuItemViewModel? CreateSubmenu(MenuItemDefinition definition, int depth)
    {
        var children = Build(definition.SubmenuId!, depth + 1);
        return children.Count == 0
            ? null
            : MenuItemViewModel.ForSubmenu(definition.Title!, definition.SubmenuId!, children, definition.When, context);
    }
}
