using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Shells;
using CodeEditor.Modules.Terminal.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Terminal.Commands;

/// <summary>
/// The Terminal menu, as in VS Code: new terminal (<c>Ctrl+Shift+`</c>), new terminal with a chosen shell, close,
/// clear, the default shell, and build and tests from <see cref="TerminalCommands"/>. <c>Ctrl+`</c> shows the panel.
/// </summary>
public sealed class TerminalPanelCommands(
    TerminalPanelViewModel panel,
    TerminalProfiles profiles,
    ICommandService commands,
    IQuickPick quickPick,
    ISettingsService settings,
    IOptionsMonitor<TerminalOptions> options,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string MenuId = "menubar.terminal";
    public const string NewWithProfileId = "terminal.newWithProfile";
    public const string KillId = "terminal.kill";
    public const string ClearId = "terminal.clear";
    public const string SelectDefaultProfileId = "terminal.selectDefaultProfile";

    // Before Help, as in VS Code.
    private const int MenuBarOrder = 5;
    private const string NewGroup = "1_new";
    private const string ManageGroup = "2_manage";
    private const string TasksGroup = "3_tasks";
    private const string SettingsGroup = "4_settings";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry registry, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(keybindings);
        ArgumentNullException.ThrowIfNull(menus);
        Add(registry, ShellCommandIds.NewTerminal, Strings.NewTerminal, (argument, _) => StartAsync(folder: argument as string));
        Add(registry, NewWithProfileId, Strings.NewTerminalWithProfile, (_, _) => PickAsync(Strings.PickShell, profile => StartAsync(profile)));
        Add(registry, KillId, Strings.KillTerminal, (_, _) => Run(() => panel.Kill()));
        Add(registry, ClearId, Strings.ClearTerminal, (_, _) => Run(panel.Clear));
        Add(registry, SelectDefaultProfileId, Strings.SelectDefaultShell, (_, _) => PickAsync(Strings.PickDefaultShell, SaveDefault));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+Shift+`"), ShellCommandIds.NewTerminal)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, MenuId, Strings.Menu, order: MenuBarOrder)));
        AddMenu(menus, ShellCommandIds.NewTerminal, NewGroup, 0, Strings.NewTerminalMenu);
        AddMenu(menus, NewWithProfileId, NewGroup, 1, Strings.NewTerminalWithProfileMenu);
        AddMenu(menus, KillId, ManageGroup, 0, Strings.KillTerminalMenu);
        AddMenu(menus, ClearId, ManageGroup, 1, Strings.ClearTerminalMenu);
        AddMenu(menus, TerminalCommands.BuildId, TasksGroup, 0, Strings.BuildMenu);
        AddMenu(menus, TerminalCommands.RunTestsId, TasksGroup, 1, Strings.RunTestsMenu);
        AddMenu(menus, SelectDefaultProfileId, SettingsGroup, 0, Strings.SelectDefaultShellMenu);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // The terminal starts before the panel shows, so showing it doesn't start a second, default one.
    private async ValueTask StartAsync(TerminalProfile? profile = null, string? folder = null)
    {
        if (panel.Start(profile, folder) is not null)
        {
            await commands.ExecuteAsync(ToolWindowAreaViewModel.ShowCommandId(TerminalModule.ToolWindowId));
            panel.RequestFocus();
        }
    }

    private ValueTask PickAsync(string placeholder, Func<TerminalProfile, ValueTask> accept)
    {
        var defaultId = profiles.Default(options.CurrentValue.DefaultProfile)?.Id;
        var available = profiles.Available();
        var items = available
            .Select(profile => new QuickPickItem(profile.Id, profile.Name)
            {
                Detail = profile.Id == defaultId ? string.Format(CultureInfo.CurrentCulture, Strings.DefaultShellDetail, profile.Executable) : profile.Executable,
            })
            .ToList();
        quickPick.Show(new QuickPickProvider(placeholder, items, item => accept(available.First(profile => profile.Id == item.Id)).AsTask()));
        return ValueTask.CompletedTask;
    }

    private ValueTask SaveDefault(TerminalProfile profile)
    {
        statusBar.Message = settings.TrySetUserValue(TerminalOptions.DefaultProfileKey, profile.Id, out var error)
            ? string.Format(CultureInfo.CurrentCulture, Strings.DefaultShellChanged, profile.Name)
            : error;
        return ValueTask.CompletedTask;
    }

    private void Add(ICommandRegistry registry, string id, string title, CommandHandler handler) =>
        _registrations.Add(registry.Register(new CommandDefinition(id, title, handler, Strings.ModuleName)));

    private void AddMenu(IMenuRegistry menus, string commandId, string group, int order, string title) =>
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuId, commandId, group, order, title: title)));

    private static ValueTask Run(Action action)
    {
        action();
        return ValueTask.CompletedTask;
    }
}
