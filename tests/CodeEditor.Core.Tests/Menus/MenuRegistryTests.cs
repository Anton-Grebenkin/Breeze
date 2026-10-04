using CodeEditor.Core.Menus;

namespace CodeEditor.Core.Tests.Menus;

public sealed class MenuRegistryTests
{
    private const string Menu = "menubar.view";

    private readonly MenuRegistry _registry = new();

    [Fact]
    public void GetItems_SortsByGroupThenOrderThenRegistration()
    {
        _registry.Register(MenuItemDefinition.ForCommand(Menu, "c", group: "2"));
        _registry.Register(MenuItemDefinition.ForCommand(Menu, "b", group: "1", order: 2));
        _registry.Register(MenuItemDefinition.ForCommand(Menu, "a", group: "1", order: 1));
        _registry.Register(MenuItemDefinition.ForCommand(Menu, "a2", group: "1", order: 1));

        Assert.Equal(["a", "a2", "b", "c"], _registry.GetItems(Menu).Select(item => item.CommandId));
    }

    [Fact]
    public void GetItems_UnknownMenu_IsEmpty()
    {
        Assert.Empty(_registry.GetItems("missing"));
    }

    [Fact]
    public void Dispose_RemovesItemAndInvalidatesCache()
    {
        var changes = 0;
        _registry.Changed += (_, _) => changes++;
        var registration = _registry.Register(MenuItemDefinition.ForCommand(Menu, "a"));
        Assert.Single(_registry.GetItems(Menu));

        registration.Dispose();

        Assert.Empty(_registry.GetItems(Menu));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Register_ItemWithCommandAndSubmenu_Throws()
    {
        var invalid = new MenuItemDefinition(Menu, "a", "sub", "Заголовок", string.Empty, 0, null);

        Assert.Throws<ArgumentException>(() => _registry.Register(invalid));
    }

    [Fact]
    public void Submenu_IsFlagged()
    {
        Assert.True(MenuItemDefinition.ForSubmenu(Menu, "sub", "Тема").IsSubmenu);
        Assert.False(MenuItemDefinition.ForCommand(Menu, "a").IsSubmenu);
    }
}
