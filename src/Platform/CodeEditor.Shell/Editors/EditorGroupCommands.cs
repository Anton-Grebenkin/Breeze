using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Keybindings;
using CodeEditor.Core.Menus;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Editor group commands (ADR 0031), as in VS Code: split editor (<c>Ctrl+\</c>) moves the active tab to a new group on
/// the right; move a tab to the neighboring group (<c>Ctrl+Alt+→</c>, <c>Ctrl+Alt+←</c>); focus group 1–4
/// (<c>Ctrl+1</c>…<c>Ctrl+4</c>, one past the last splits); close group; join groups. Available from the View menu,
/// the tab menu and the palette; tabs can also be dragged.
/// </summary>
public sealed class EditorGroupCommands(EditorAreaViewModel editors, ICommandService commandService) : IDisposable
{
    public const string SplitId = "workbench.action.splitEditor";
    public const string MoveToNextGroupId = "workbench.action.moveEditorToNextGroup";
    public const string MoveToPreviousGroupId = "workbench.action.moveEditorToPreviousGroup";
    public const string FocusGroupPrefix = "workbench.action.focusEditorGroup.";
    public const string CloseGroupId = "workbench.action.closeEditorGroup";
    public const string JoinGroupsId = "workbench.action.joinAllGroups";

    private const string ViewGroup = "5_editorLayout";
    private const string TabGroup = "8_groups";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings, IMenuRegistry menus)
    {
        var editorOpen = ContextExpression.Parse(EditorAreaViewModel.EditorOpenContextKey);
        Add(commands, SplitId, Strings.SplitEditor, Split, editorOpen);
        Add(commands, MoveToNextGroupId, Strings.MoveEditorToNextGroup, () => MoveToNeighbor(1), editorOpen);
        Add(commands, MoveToPreviousGroupId, Strings.MoveEditorToPreviousGroup, () => MoveToNeighbor(-1), editorOpen);
        Add(commands, CloseGroupId, Strings.CloseEditorGroup, () => _ = editors.CloseManyAsync([.. editors.ActiveGroup.Tabs]), editorOpen);
        Add(commands, JoinGroupsId, Strings.JoinEditorGroups, JoinGroups, editorOpen);
        for (var number = 1; number <= EditorGroups.Max; number++)
        {
            var group = number;
            _registrations.Add(commands.Register(new CommandDefinition(FocusGroupPrefix + number,
                string.Format(CultureInfo.CurrentCulture, Strings.FocusEditorGroup, number), (_, cancellationToken) => FocusGroupAsync(group, cancellationToken), Strings.CategoryView)));
            Bind(keybindings, string.Create(CultureInfo.InvariantCulture, $"Ctrl+{number}"), FocusGroupPrefix + number);
        }

        Bind(keybindings, "Ctrl+\\", SplitId);
        Bind(keybindings, "Ctrl+Alt+Right", MoveToNextGroupId);
        Bind(keybindings, "Ctrl+Alt+Left", MoveToPreviousGroupId);
        string[] viewItems = [SplitId, MoveToNextGroupId, MoveToPreviousGroupId, JoinGroupsId, CloseGroupId];
        for (var order = 0; order < viewItems.Length; order++)
        {
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.View, viewItems[order], ViewGroup, order)));
        }

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, SplitId, TabGroup, 0)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(EditorCommands.TabContextMenuId, MoveToNextGroupId, TabGroup, 1)));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // A single tab isn't split: moving it out would close the emptied group.
    private void Split()
    {
        if (editors.Active is { } tab && editors.GroupOf(tab) is { Tabs.Count: > 1 } group && editors.AddGroup(group) is { } right)
        {
            editors.MoveToGroup(tab, right);
        }
    }

    // A missing neighbor group is created if the source group keeps at least one tab.
    private void MoveToNeighbor(int delta)
    {
        var group = editors.ActiveGroup;
        var index = editors.Groups.IndexOf(group) + delta;
        var target = index >= 0 && index < editors.Groups.Count
            ? editors.Groups[index]
            : group.Tabs.Count > 1 ? editors.AddGroup(group, before: delta < 0) : null;
        if (editors.Active is { } tab && target is not null)
        {
            editors.MoveToGroup(tab, target);
        }
    }

    private void JoinGroups()
    {
        foreach (var tab in editors.Groups.Skip(1).SelectMany(group => group.Tabs).ToList())
        {
            editors.MoveToGroup(tab, editors.Groups[0]);
        }
    }

    // One past the last group splits, as in VS Code; then focus moves to that group's tab.
    private async ValueTask FocusGroupAsync(int number, CancellationToken cancellationToken)
    {
        if (number == editors.Groups.Count + 1)
        {
            Split();
        }

        if (number > editors.Groups.Count)
        {
            return;
        }

        editors.Activate(editors.Groups[number - 1]);
        if (editors.Active?.Editor is IFocusableContent content)
        {
            content.RequestFocus();
            return;
        }

        await commandService.ExecuteAsync(ShellCommandIds.FocusActiveEditor, cancellationToken: cancellationToken);
    }

    private void Add(ICommandRegistry commands, string id, string title, Action action, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            action();
            return ValueTask.CompletedTask;
        }, Strings.CategoryView, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId)));
}
