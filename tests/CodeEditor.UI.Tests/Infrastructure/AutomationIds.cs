namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>AutomationIds of window elements the UI tests rely on.</summary>
internal static class AutomationIds
{
    public const string StatusBarMessage = "StatusBar.Message";
    public const string TitleBarSearch = "TitleBar.Search";
    public const string MenuBar = "MenuBar";
    public const string CaptionMinimize = "Caption.Minimize";
    public const string CaptionMaximize = "Caption.Maximize";
    public const string CaptionClose = "Caption.Close";
    public const string ActivityBarManage = "ActivityBar.Manage";
    public const string Welcome = "Welcome";

    public const string OutputText = "Output.Text";
    public const string PanelClose = "Panel.Close";

    public const string Palette = "CommandPalette";
    public const string PaletteQuery = "CommandPalette.Query";
    public const string PaletteEmpty = "CommandPalette.Empty";

    public static string PaletteItem(string commandId) => $"CommandPalette.Item.{commandId}";

    public static string MenuItem(string id) => $"Menu.{id}";

    public static string WelcomeItem(string commandId) => $"Welcome.{commandId}";
}
