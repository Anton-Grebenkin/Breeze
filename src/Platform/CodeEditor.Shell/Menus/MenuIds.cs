namespace CodeEditor.Shell.Menus;

/// <summary>Shell menu ids. Modules add items to them through <c>IMenuRegistry</c>.</summary>
public static class MenuIds
{
    /// <summary>The menu bar in the title bar; its items are top-level submenus.</summary>
    public const string MenuBar = "menubar";

    public const string File = "menubar.file";
    public const string Edit = "menubar.edit";
    public const string View = "menubar.view";
    public const string Go = "menubar.go";
    public const string Help = "menubar.help";

    /// <summary>Theme submenu, in both View and Manage.</summary>
    public const string Theme = "menubar.view.theme";

    /// <summary>Language submenu, next to Theme in View and Manage.</summary>
    public const string Language = "menubar.view.language";

    /// <summary>Menu of the Manage button at the bottom of the activity bar.</summary>
    public const string Manage = "activitybar.manage";
}
