using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.TextEditor.Commands;

/// <summary>
/// Agent change commands in the active editor: accept or reject the hunk at the caret or all in the file, go to the
/// next or previous hunk. Keys as in Copilot and VS Code's diff: <c>F7</c>/<c>Shift+F7</c> between hunks;
/// accept/reject use <c>Ctrl+Alt</c> to avoid clashing with undo and menu mnemonics.
/// </summary>
public sealed class AgentChangeCommands(EditorAreaViewModel editors) : IDisposable
{
    public const string AcceptHunkId = "editor.acceptAgentChange";
    public const string RejectHunkId = "editor.rejectAgentChange";
    public const string AcceptAllId = "editor.acceptAgentChanges";
    public const string RejectAllId = "editor.rejectAgentChanges";
    public const string NextId = "editor.nextAgentChange";
    public const string PreviousId = "editor.previousAgentChange";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var editorOpen = ContextExpression.Parse(EditorAreaViewModel.EditorOpenContextKey);
        var editorFocus = ContextExpression.Parse(TextEditorCommands.EditorFocusContextKey);

        Add(commands, AcceptHunkId, Strings.AcceptAgentChange, (changes, line) => changes.AcceptHunkCommand.ExecuteAsync(changes.HunkAt(line)), editorOpen);
        Add(commands, RejectHunkId, Strings.RejectAgentChange, (changes, line) => { changes.RejectHunkCommand.Execute(changes.HunkAt(line)); return Task.CompletedTask; }, editorOpen);
        Add(commands, AcceptAllId, Strings.AcceptAgentChanges, (changes, _) => changes.AcceptAllCommand.ExecuteAsync(null), editorOpen);
        Add(commands, RejectAllId, Strings.RejectAgentChanges, (changes, _) => changes.RejectAllCommand.ExecuteAsync(null), editorOpen);
        Add(commands, NextId, Strings.NextAgentChange, (changes, line) => { changes.NextCommand.Execute(line); return Task.CompletedTask; }, editorOpen);
        Add(commands, PreviousId, Strings.PreviousAgentChange, (changes, line) => { changes.PreviousCommand.Execute(line); return Task.CompletedTask; }, editorOpen);

        Bind(keybindings, "Ctrl+Alt+Y", AcceptHunkId, editorFocus);
        Bind(keybindings, "Ctrl+Alt+N", RejectHunkId, editorFocus);
        Bind(keybindings, "Ctrl+Alt+Shift+Y", AcceptAllId, editorFocus);
        Bind(keybindings, "Ctrl+Alt+Shift+N", RejectAllId, editorFocus);
        Bind(keybindings, "F7", NextId, editorFocus);
        Bind(keybindings, "Shift+F7", PreviousId, editorFocus);

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, AcceptHunkId, "6_agentChanges", order: 1, title: Strings.MenuAcceptAgentChange)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, RejectHunkId, "6_agentChanges", order: 2, title: Strings.MenuRejectAgentChange)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, AcceptAllId, "6_agentChanges", order: 3, title: Strings.MenuAcceptAgentChanges)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, RejectAllId, "6_agentChanges", order: 4, title: Strings.MenuRejectAgentChanges)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, NextId, "6_agentChanges", order: 5, title: Strings.MenuNextAgentChange)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Edit, PreviousId, "6_agentChanges", order: 6, title: Strings.MenuPreviousAgentChange)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // Acts on the active text editor's changes; a no-op when there are none.
    private void Add(ICommandRegistry commands, string id, string title, Func<AgentChangesViewModel, int, Task> action, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (_, _) =>
        {
            if (editors.Active?.Editor is TextEditorViewModel { AgentChanges: { HasChanges: true } changes } editor)
            {
                await action(changes, editor.CaretLine);
            }
        }, Strings.CategoryEdit, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));
}
