namespace CodeEditor.Core.Menus;

/// <summary>
/// Menu item registry. The menu bar, context menus and the Manage menu are built from it.
/// </summary>
public interface IMenuRegistry
{
    event EventHandler? Changed;

    IDisposable Register(MenuItemDefinition item);

    /// <summary>Menu items sorted by group, order and registration time.</summary>
    IReadOnlyList<MenuItemDefinition> GetItems(string menuId);
}
