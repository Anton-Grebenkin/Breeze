using CodeEditor.Core.Common;

namespace CodeEditor.Core.Menus;

/// <summary>
/// Menu items by menu id. A menu's sorted list is cached until the next change.
/// Not thread-safe: used from the UI thread.
/// </summary>
public sealed class MenuRegistry : IMenuRegistry
{
    private readonly Dictionary<string, List<MenuItemDefinition>> _itemsByMenu = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MenuItemDefinition[]> _sorted = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public IDisposable Register(MenuItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.MenuId);
        if (item.CommandId is null == item.SubmenuId is null)
        {
            throw new ArgumentException("A menu item is either a command or a submenu.", nameof(item));
        }

        if (!_itemsByMenu.TryGetValue(item.MenuId, out var items))
        {
            items = [];
            _itemsByMenu[item.MenuId] = items;
        }

        items.Add(item);
        OnChanged(item.MenuId);

        return new DisposableAction(() =>
        {
            // By reference: records compare by value, but exactly this registration must be removed.
            var position = items.FindLastIndex(candidate => ReferenceEquals(candidate, item));
            if (position >= 0)
            {
                items.RemoveAt(position);
                OnChanged(item.MenuId);
            }
        });
    }

    public IReadOnlyList<MenuItemDefinition> GetItems(string menuId)
    {
        if (_sorted.TryGetValue(menuId, out var cached))
        {
            return cached;
        }

        if (!_itemsByMenu.TryGetValue(menuId, out var items))
        {
            return Array.Empty<MenuItemDefinition>();
        }

        // Stable sort: items with equal group and order keep their registration order.
        var sorted = items
            .OrderBy(item => item.Group, StringComparer.Ordinal)
            .ThenBy(item => item.Order)
            .ToArray();

        _sorted[menuId] = sorted;
        return sorted;
    }

    private void OnChanged(string menuId)
    {
        _sorted.Remove(menuId);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
