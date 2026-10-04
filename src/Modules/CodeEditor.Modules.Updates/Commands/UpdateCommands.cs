using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Updates.Resources;
using CodeEditor.Modules.Updates.Services;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.Updates.Commands;

/// <summary>"Check for Updates…" in the Help menu and palette; "Restart to Update" also sits in the status bar.</summary>
public sealed class UpdateCommands(UpdateService updates) : IDisposable
{
    public const string CheckId = "update.check";
    public const string RestartId = "update.restart";

    // Between the project links and "About", as in VS Code.
    private const string UpdatesGroup = "8_updates";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(menus);
        _registrations.Add(commands.Register(new CommandDefinition(CheckId, Strings.CheckCommand, async (_, _) => await updates.CheckAsync(interactive: true), Strings.Category)));
        _registrations.Add(commands.Register(new CommandDefinition(RestartId, Strings.RestartCommand, async (_, _) => await updates.RestartToUpdateAsync(), Strings.Category)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Help, CheckId, UpdatesGroup, title: Strings.CheckMenu)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }
}
