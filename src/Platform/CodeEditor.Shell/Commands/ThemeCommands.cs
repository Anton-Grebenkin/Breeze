using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Theming;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Theme commands, their keybinding and Theme submenu items. The choice is saved to <c>workbench.colorTheme</c> in
/// user settings, as in VS Code.
/// </summary>
public sealed class ThemeCommands(IThemeService themes, ISettingsService settings, StatusBarViewModel statusBar) : IDisposable
{
    public const string SettingKey = "workbench.colorTheme";

    private const string ToggleKeys = "Ctrl+K Ctrl+T";
    private const string ThemesGroup = "1_themes";
    private const string ToggleGroup = "2_toggle";

    private readonly List<IDisposable> _registrations = [];

    public static string DisplayName(ThemeKind theme) => theme switch
    {
        ThemeKind.Dark => Strings.ThemeNameDark,
        ThemeKind.Light => Strings.ThemeNameLight,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var category = Strings.CategoryAppearance;
        _registrations.Add(commands.Register(new CommandDefinition(
            ShellCommandIds.ToggleTheme, Strings.ToggleTheme, (_, _) => Apply(Opposite(themes.Current)), category)));
        _registrations.Add(commands.Register(new CommandDefinition(
            ShellCommandIds.DarkTheme, Strings.DarkTheme, (_, _) => Apply(ThemeKind.Dark), category)));
        _registrations.Add(commands.Register(new CommandDefinition(
            ShellCommandIds.LightTheme, Strings.LightTheme, (_, _) => Apply(ThemeKind.Light), category)));

        _registrations.Add(keybindings.Register(
            new KeybindingDefinition(KeySequence.Parse(ToggleKeys), ShellCommandIds.ToggleTheme)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Theme, ShellCommandIds.DarkTheme, ThemesGroup, order: 1, title: Strings.DarkThemeMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Theme, ShellCommandIds.LightTheme, ThemesGroup, order: 2, title: Strings.LightThemeMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Theme, ShellCommandIds.ToggleTheme, ToggleGroup, title: Strings.ToggleThemeMenu)));
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    private ValueTask Apply(ThemeKind theme)
    {
        // The theme applies immediately; if settings.json is broken, the choice isn't saved and the status bar says so.
        themes.Apply(theme);
        statusBar.Message = settings.TrySetUserValue(SettingKey, ThemeNames.ToName(theme), out var error)
            ? string.Format(CultureInfo.CurrentCulture, Strings.ThemeChanged, DisplayName(theme))
            : error;
        return ValueTask.CompletedTask;
    }

    private static ThemeKind Opposite(ThemeKind theme) => theme == ThemeKind.Dark ? ThemeKind.Light : ThemeKind.Dark;
}
