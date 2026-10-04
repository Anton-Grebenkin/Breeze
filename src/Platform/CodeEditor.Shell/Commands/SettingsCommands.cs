using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Settings;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Settings commands as in VS Code: <c>Ctrl+,</c> opens the user <c>settings.json</c>, a separate command opens the
/// folder's one, <c>Ctrl+K Ctrl+S</c> opens <c>keybindings.json</c>. File errors show in the status bar on save.
/// </summary>
public sealed class SettingsCommands(
    ISettingsService settings,
    UserKeybindings userKeybindings,
    ICommandService commandService,
    StatusBarViewModel statusBar) : IDisposable
{
    public const string OpenUserSettingsId = "workbench.openSettingsJson";
    public const string OpenWorkspaceSettingsId = "workbench.openWorkspaceSettingsJson";
    public const string OpenKeybindingsId = "workbench.openGlobalKeybindingsFile";

    private const string SettingsGroup = "3_settings";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var category = Strings.CategorySettings;
        _registrations.Add(commands.Register(new CommandDefinition(
            OpenUserSettingsId, Strings.OpenUserSettings, (_, _) => OpenAsync(settings.EnsureUserSettingsFile()), category)));
        _registrations.Add(commands.Register(new CommandDefinition(
            OpenWorkspaceSettingsId, Strings.OpenWorkspaceSettings, (_, _) => OpenAsync(settings.EnsureWorkspaceSettingsFile()), category,
            ContextExpression.Parse(IWorkspace.OpenContextKey))));

        _registrations.Add(commands.Register(new CommandDefinition(
            OpenKeybindingsId, Strings.OpenKeybindings, (_, _) => OpenAsync(userKeybindings.EnsureFile()), category)));

        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+,"), OpenUserSettingsId)));
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Ctrl+K Ctrl+S"), OpenKeybindingsId)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, OpenUserSettingsId, SettingsGroup, order: 1, title: Strings.SettingsMenuManage)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, OpenWorkspaceSettingsId, SettingsGroup, order: 2, title: Strings.WorkspaceSettingsMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, OpenKeybindingsId, SettingsGroup, order: 3, title: Strings.KeybindingsMenu)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, OpenUserSettingsId, "8_settings", order: 1, title: Strings.SettingsMenuFile)));

        settings.Changed += OnSettingsChanged;
        userKeybindings.Changed += OnKeybindingsChanged;
    }

    public void Dispose()
    {
        settings.Changed -= OnSettingsChanged;
        userKeybindings.Changed -= OnKeybindingsChanged;
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private async ValueTask OpenAsync(string? path)
    {
        if (path is not null)
        {
            await commandService.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (settings.Error is { } error)
        {
            statusBar.Message = error;
        }
    }

    private void OnKeybindingsChanged(object? sender, EventArgs e)
    {
        if (userKeybindings.Errors.Count > 0)
        {
            statusBar.Message = $"{UserKeybindings.FileName}: {userKeybindings.Errors[0]}";
        }
    }
}
