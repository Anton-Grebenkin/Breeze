using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.TextEditor.Commands;

/// <summary>
/// Text editor commands: undo and redo via the active document's buffer, font zoom, go to line and focus.
/// Menu items go to Edit. Font zoom has no keys, as in VS Code: <c>Ctrl+=</c> zooms the interface, Ctrl + wheel over the
/// editor zooms its font. Clipboard and find commands are added by the view module.
/// </summary>
public sealed class TextEditorCommands(EditorAreaViewModel editors, EditorFontZoom fontZoom) : IDisposable
{
    /// <summary>Set by the view while the editor text has focus.</summary>
    public const string EditorFocusContextKey = EditorContextKeys.TextFocus;

    public const string UndoId = "editor.undo";
    public const string RedoId = "editor.redo";
    public const string ZoomInId = "editor.zoomIn";
    public const string ZoomOutId = "editor.zoomOut";
    public const string ZoomResetId = "editor.zoomReset";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var editorOpen = ContextExpression.Parse(EditorAreaViewModel.EditorOpenContextKey);
        var editorFocus = ContextExpression.Parse(EditorFocusContextKey);

        Add(commands, UndoId, Strings.Undo, Strings.CategoryEdit, () => editors.ActiveDocument?.Buffer.Undo(), editorOpen);
        Add(commands, RedoId, Strings.Redo, Strings.CategoryEdit, () => editors.ActiveDocument?.Buffer.Redo(), editorOpen);
        Add(commands, ZoomInId, Strings.ZoomIn, Strings.CategoryView, fontZoom.ZoomIn, null);
        Add(commands, ZoomOutId, Strings.ZoomOut, Strings.CategoryView, fontZoom.ZoomOut, null);
        Add(commands, ZoomResetId, Strings.ZoomReset, Strings.CategoryView, fontZoom.Reset, null);
        _registrations.Add(commands.Register(new CommandDefinition(ShellCommandIds.EditorGoToLine, Strings.GoToLine, GoToLine, Strings.CategoryEdit, editorOpen)));
        Add(commands, ShellCommandIds.FocusActiveEditor, Strings.FocusEditor, Strings.CategoryView, () => ActiveEditor?.RequestFocus(), editorOpen);

        // Undo and redo only in the text: input boxes keep their own undo.
        Bind(keybindings, "Ctrl+Z", UndoId, editorFocus);
        Bind(keybindings, "Ctrl+Y", RedoId, editorFocus);
        Bind(keybindings, "Ctrl+Shift+Z", RedoId, editorFocus);

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, UndoId, "1_undo", order: 1, title: Strings.MenuUndo)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, RedoId, "1_undo", order: 2, title: Strings.MenuRedo)));
    }

    private TextEditorViewModel? ActiveEditor => editors.Active?.Editor as TextEditorViewModel;

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // The argument is a 1-based line (palette ":" and "file:line") or a match location from file search.
    private ValueTask GoToLine(object? argument, CancellationToken cancellationToken)
    {
        var location = argument switch
        {
            int line and > 0 => new EditorLocation(line),
            EditorLocation { Line: > 0, Column: > 0 } requested => requested,
            _ => null,
        };

        if (location is not null)
        {
            ActiveEditor?.Reveal(location);
        }

        return ValueTask.CompletedTask;
    }

    private void Add(ICommandRegistry commands, string id, string title, string category, Action handler, ContextExpression? when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            handler();
            return ValueTask.CompletedTask;
        }, category, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression? when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));
}
