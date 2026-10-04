using System.Windows;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;

namespace CodeEditor.Shell.Wpf.Commands;

/// <summary>
/// App window commands: exit and restart. They live in Shell.Wpf because they work with the WPF window.
/// </summary>
public sealed class WindowCommands(Application application, AppRestart restart) : IDisposable
{
    public const string QuitCommandId = "workbench.quit";

    private const string ExitGroup = "9_exit";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        _registrations.Add(commands.Register(new CommandDefinition(QuitCommandId, Strings.Exit, Quit, Strings.CategoryFile)));
        _registrations.Add(commands.Register(new CommandDefinition(ShellCommandIds.Restart, Strings.RestartCommand, Restart, Strings.CategoryFile)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, ShellCommandIds.Restart, ExitGroup, order: -1, title: Strings.RestartMenu)));

        // Windows handles Alt+F4 itself; the binding makes the shortcut visible in the menu and palette.
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse("Alt+F4"), QuitCommandId)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.File, QuitCommandId, ExitGroup, title: Strings.ExitMenu)));
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    // Close the main window rather than exit the process, so it can ask about unsaved files.
    private ValueTask Quit(object? argument, CancellationToken cancellationToken)
    {
        application.MainWindow?.Close();
        return ValueTask.CompletedTask;
    }

    // The same exit plus a restart request; the session restores the folder and tabs.
    private ValueTask Restart(object? argument, CancellationToken cancellationToken)
    {
        restart.Request();
        return Quit(argument, cancellationToken);
    }
}
