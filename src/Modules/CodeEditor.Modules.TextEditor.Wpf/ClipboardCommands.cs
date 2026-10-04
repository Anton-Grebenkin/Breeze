using System.Windows;
using System.Windows.Input;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.TextEditor.Wpf;

/// <summary>
/// Edit menu: cut, copy, paste, select all, find, replace as WPF routed commands on the focused element. The element
/// (editor, input box) handles the keys itself, so the keybindings are display-only hints.
/// </summary>
public sealed class ClipboardCommands : IDisposable
{
    public const string CutId = "editor.cut";
    public const string CopyId = "editor.copy";
    public const string PasteId = "editor.paste";
    public const string SelectAllId = "editor.selectAll";
    public const string FindId = "editor.find";
    public const string ReplaceId = "editor.replace";

    private static readonly (string Id, string Title, string MenuTitle, RoutedUICommand Command, string Keys, string Group)[] Items =
    [
        (CutId, Strings.Cut, Strings.MenuCut, ApplicationCommands.Cut, "Ctrl+X", "2_clipboard"),
        (CopyId, Strings.Copy, Strings.MenuCopy, ApplicationCommands.Copy, "Ctrl+C", "2_clipboard"),
        (PasteId, Strings.Paste, Strings.MenuPaste, ApplicationCommands.Paste, "Ctrl+V", "2_clipboard"),
        (SelectAllId, Strings.SelectAll, Strings.MenuSelectAll, ApplicationCommands.SelectAll, "Ctrl+A", "3_select"),
        (FindId, Strings.FindInFile, Strings.MenuFind, ApplicationCommands.Find, "Ctrl+F", "4_find"),
        (ReplaceId, Strings.ReplaceInFile, Strings.MenuReplace, ApplicationCommands.Replace, "Ctrl+H", "4_find"),
    ];

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            var (id, title, menuTitle, command, keys, group) = Items[i];
            _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
            {
                Execute(command);
                return ValueTask.CompletedTask;
            }, Strings.CategoryEdit)));
            _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), id, DisplayOnly: true)));
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, id, group, i, menuTitle)));
        }
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    /// <summary>Sends the command to the keyboard-focused element (WPF menus don't take focus).</summary>
    private static void Execute(RoutedUICommand command)
    {
        if (Keyboard.FocusedElement is { } target && command.CanExecute(null, target))
        {
            command.Execute(null, target);
        }
        else if (Application.Current?.MainWindow is { } window && command.CanExecute(null, window))
        {
            command.Execute(null, window);
        }
    }
}
