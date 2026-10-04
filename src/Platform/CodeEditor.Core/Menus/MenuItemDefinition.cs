using CodeEditor.Core.Context;

namespace CodeEditor.Core.Menus;

/// <summary>
/// Menu item: a command or a submenu. Items of one group are adjacent; groups are separated by a line.
/// </summary>
/// <param name="MenuId">The menu that contains the item, e.g. <c>menubar.view</c>.</param>
/// <param name="Title">Title; <c>null</c> for a command uses the command title. "_" marks the access key.</param>
/// <param name="When">Visibility condition; the command's own <c>when</c> controls whether it is enabled.</param>
public sealed record MenuItemDefinition(
    string MenuId,
    string? CommandId,
    string? SubmenuId,
    string? Title,
    string Group,
    int Order,
    ContextExpression? When)
{
    public bool IsSubmenu => SubmenuId is not null;

    /// <summary>Command argument, e.g. a path in "Open Recent".</summary>
    public object? Argument { get; init; }

    public static MenuItemDefinition ForCommand(
        string menuId,
        string commandId,
        string group = "",
        int order = 0,
        string? title = null,
        ContextExpression? when = null,
        object? argument = null) =>
        new(menuId, commandId, SubmenuId: null, title, group, order, when) { Argument = argument };

    public static MenuItemDefinition ForSubmenu(
        string menuId,
        string submenuId,
        string title,
        string group = "",
        int order = 0,
        ContextExpression? when = null) =>
        new(menuId, CommandId: null, submenuId, title, group, order, when);
}
