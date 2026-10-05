using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Integration;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Settings;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Options;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Turns on and off what Breeze adds to Windows Explorer. The commands only change settings; the app applies them to
/// the registry (ADR 0046). An uninstalled build has nothing to register, so it gets no commands.
/// </summary>
public sealed class WindowsIntegrationCommands(
    IWindowsIntegration integration,
    ISettingsService settings,
    IOptionsMonitor<WindowsIntegrationOptions> options,
    IQuickPick quickPick,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string ConfigureId = "windowsIntegration.configure";
    public const string EnableFileTypesId = "windowsIntegration.enableFileTypes";
    public const string DisableFileTypesId = "windowsIntegration.disableFileTypes";

    // Next to the settings files in the Manage menu.
    private const string SettingsGroup = "3_settings";
    private const int MenuOrder = 4;
    private const string FileTypesItem = "fileTypes";
    private const string ContextMenuItem = "contextMenu";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(menus);
        if (!integration.IsAvailable)
        {
            return;
        }

        Add(commands, ConfigureId, Strings.WindowsIntegrationCommand, (_, _) => Configure());
        Add(commands, EnableFileTypesId, Strings.EnableFileTypesCommand, (_, _) => SetFileTypes(true));
        Add(commands, DisableFileTypesId, Strings.DisableFileTypesCommand, (_, _) => SetFileTypes(false));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(
            MenuIds.Manage, ConfigureId, SettingsGroup, order: MenuOrder, title: Strings.WindowsIntegrationMenu)));
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    private ValueTask Configure()
    {
        var current = options.CurrentValue;
        QuickPickItem[] items =
        [
            new(FileTypesItem, Strings.FileTypesOption) { Detail = State(current.FileTypes == true) },
            new(ContextMenuItem, Strings.ContextMenuOption) { Detail = State(current.ContextMenu) },
        ];
        quickPick.Show(new QuickPickProvider(Strings.WindowsIntegrationPlaceholder, items, item => Toggle(item.Id).AsTask()));
        return ValueTask.CompletedTask;
    }

    private ValueTask Toggle(string item) =>
        item == FileTypesItem
            ? SetFileTypes(options.CurrentValue.FileTypes != true)
            : SetContextMenu(!options.CurrentValue.ContextMenu);

    private ValueTask SetFileTypes(bool on) =>
        Set(WindowsIntegrationOptions.FileTypesKey, on, on ? Strings.FileTypesOn : Strings.FileTypesOff);

    private ValueTask SetContextMenu(bool on) =>
        Set(WindowsIntegrationOptions.ContextMenuKey, on, on ? Strings.ContextMenuOn : Strings.ContextMenuOff);

    private ValueTask Set(string key, bool value, string done)
    {
        statusBar.Message = settings.TrySetUserValue(key, value, out var error) ? done : error;
        return ValueTask.CompletedTask;
    }

    private static string State(bool on) => on ? Strings.On : Strings.Off;

    private void Add(ICommandRegistry commands, string id, string title, CommandHandler handler) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, handler)));
}
