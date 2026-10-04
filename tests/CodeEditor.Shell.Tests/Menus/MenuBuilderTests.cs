using CodeEditor.Core.Context;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Tests.Infrastructure;

namespace CodeEditor.Shell.Tests.Menus;

public sealed class MenuBuilderTests
{
    private const string Menu = "test.menu";
    private const string Submenu = "test.submenu";

    private readonly ShellFixture _shell = new();

    [Fact]
    public void Build_SeparatesGroupsAndSkipsUnknownCommands()
    {
        _shell.RegisterCommand("a", "Альфа");
        _shell.RegisterCommand("b", "Бета");
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "b", group: "2"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "a", group: "1"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "missing", group: "1"));

        var items = _shell.MenuBuilder.Build(Menu);

        Assert.Equal(["Альфа", "—", "Бета"], items.Select(item => item.ToString()));
    }

    [Fact]
    public void Build_OrdersWithinGroupAndPrefersMenuTitle()
    {
        _shell.RegisterCommand("a", "Альфа");
        _shell.RegisterCommand("b", "Бета");
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "a", order: 2));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "b", order: 1, title: "_Б"));

        var items = _shell.MenuBuilder.Build(Menu);

        Assert.Equal(["_Б", "Альфа"], items.Select(item => item.Header));
    }

    [Fact]
    public void Build_HidesEmptySubmenus()
    {
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(Menu, Submenu, "Пустое"));

        Assert.Empty(_shell.MenuBuilder.Build(Menu));
    }

    [Fact]
    public void Build_NestsSubmenuItemsWithShortcuts()
    {
        _shell.RegisterCommand("a", "Альфа");
        _shell.Bind("Ctrl+K Ctrl+A", "a");
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(Menu, Submenu, "Вложенное"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Submenu, "a"));

        var submenu = Assert.Single(_shell.MenuBuilder.Build(Menu));
        var item = Assert.Single(submenu.Items);

        Assert.Equal("Menu.test.submenu", submenu.AutomationId);
        Assert.Equal("Ctrl+K Ctrl+A", item.InputGestureText);
    }

    [Fact]
    public void Build_SubmenuCycle_DoesNotHang()
    {
        _shell.RegisterCommand("a", "Альфа");
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "a"));
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(Menu, Menu, "Сам в себе"));

        var items = _shell.MenuBuilder.Build(Menu);

        Assert.NotEmpty(items);
    }

    [Fact]
    public void OpeningSubmenu_RefreshesVisibilityAndAvailability()
    {
        _shell.RegisterCommand("save", "Сохранить", when: "editorFocus");
        _shell.RegisterCommand("close", "Закрыть");
        _shell.Menus.Register(MenuItemDefinition.ForSubmenu(Menu, Submenu, "Файл"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Submenu, "save"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Submenu, "close", when: ContextExpression.Parse("workspaceOpen")));
        var file = Assert.Single(_shell.MenuBuilder.Build(Menu));
        var (save, close) = (file.Items[0], file.Items[1]);

        Assert.False(save.Command!.CanExecute(null));
        Assert.False(close.IsVisible);

        _shell.Context.Set("editorFocus", true);
        _shell.Context.Set("workspaceOpen", true);
        file.IsOpen = true;

        Assert.True(save.Command.CanExecute(null));
        Assert.True(close.IsVisible);
    }

    // A group hidden by its condition leaves no double separator; a menu never starts or ends with one.
    [Fact]
    public void Separators_ShowOnlyBetweenVisibleItems()
    {
        var hidden = ContextExpression.Parse("selected");
        _shell.RegisterCommand("a", "Альфа");
        _shell.RegisterCommand("b", "Бета");
        _shell.RegisterCommand("c", "Гамма");
        _shell.RegisterCommand("d", "Дельта");
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "a", group: "1", when: hidden));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "b", group: "2"));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "c", group: "3", when: hidden));
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "d", group: "4"));
        using var menu = _shell.MenuFactory.Create(Menu);

        Assert.Equal(["Бета", "—", "Дельта"], Visible(menu));

        _shell.Context.Set("selected", true);
        menu.Refresh();

        Assert.Equal(["Альфа", "—", "Бета", "—", "Гамма", "—", "Дельта"], Visible(menu));
    }

    [Fact]
    public void MenuViewModel_RebuildsWhenRegistryChanges()
    {
        using var menu = _shell.MenuFactory.Create(Menu);
        Assert.Empty(menu.Items);

        _shell.RegisterCommand("a", "Альфа");
        _shell.Menus.Register(MenuItemDefinition.ForCommand(Menu, "a"));

        Assert.Equal(["Альфа"], menu.Items.Select(item => item.Header));
    }

    private static IEnumerable<string> Visible(MenuViewModel menu) =>
        menu.Items.Where(item => item.IsVisible).Select(item => item.ToString());
}
