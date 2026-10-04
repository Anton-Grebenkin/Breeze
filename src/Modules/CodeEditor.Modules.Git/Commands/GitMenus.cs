using CodeEditor.Core.Context;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Shell.Menus;

namespace CodeEditor.Modules.Git.Commands;

/// <summary>
/// Git menus: "Git" in the menu bar (as in Visual Studio), also shown as "…" in the panel header, plus context menus
/// for files and groups in the change list. Items depend on the selected row via <see cref="GitContextKeys"/>.
/// </summary>
public sealed class GitMenus : IDisposable
{
    /// <summary>"Git" in the menu bar and "…" in the panel header.</summary>
    public const string Main = "menubar.git";

    /// <summary>Context menu of a file in the change list.</summary>
    public const string File = "git.changes.file";

    /// <summary>Context menu of a group header.</summary>
    public const string Group = "git.changes.group";

    // Between "Go" (4) and "Help" (5): a later registration with the same order goes after.
    private const int MenuBarOrder = 4;

    private readonly List<IDisposable> _registrations = [];

    public void Register(IMenuRegistry menus)
    {
        ArgumentNullException.ThrowIfNull(menus);
        _registrations.Add(menus.Register(MenuItemDefinition.ForSubmenu(MenuIds.MenuBar, Main, Strings.GitMenu, order: MenuBarOrder)));
        RegisterMain(menus);
        RegisterFileMenu(menus);
        RegisterGroupMenu(menus);
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void RegisterMain(IMenuRegistry menus)
    {
        var notRepository = When($"{GitContextKeys.State} == '{GitContextKeys.NotRepository}'");
        (string Command, string Group, ContextExpression? When)[] items =
        [
            (GitCommandIds.Commit, "1_commit", null),
            (GitCommandIds.Pull, "2_sync", null),
            (GitCommandIds.Push, "2_sync", null),
            (GitCommandIds.Fetch, "2_sync", null),
            (GitCommandIds.Checkout, "3_branch", null),
            (GitCommandIds.CreateBranch, "3_branch", null),
            (GitCommandIds.StageAll, "4_changes", null),
            (GitCommandIds.UnstageAll, "4_changes", null),
            (GitCommandIds.ShowHistory, "5_history", null),
            (GitCommandIds.Refresh, "6_repository", null),
            (GitCommandIds.Init, "6_repository", notRepository),
        ];
        Add(menus, Main, items);
    }

    // Items of one menu group show or hide together; otherwise dangling separators remain.
    private void RegisterFileMenu(IMenuRegistry menus)
    {
        var group = GitContextKeys.ResourceGroup;
        (string Command, string Group, ContextExpression? When)[] items =
        [
            (GitCommandIds.OpenChanges, "1_open", null),
            (GitCommandIds.OpenFile, "1_open", When($"!{GitContextKeys.ResourceDeleted}")),
            (GitCommandIds.Stage, "2_index", When($"{group} != 'staged'")),
            (GitCommandIds.Unstage, "2_index", When($"{group} == 'staged'")),
            (GitCommandIds.Discard, "2_index", When($"{group} == 'changes'")),
        ];
        Add(menus, File, items);
    }

    private void RegisterGroupMenu(IMenuRegistry menus)
    {
        var group = GitContextKeys.ResourceGroup;
        (string Command, string Group, ContextExpression? When)[] items =
        [
            (GitCommandIds.StageAll, "1_index", When($"{group} != 'staged'")),
            (GitCommandIds.UnstageAll, "1_index", When($"{group} == 'staged'")),
        ];
        Add(menus, Group, items);
    }

    private void Add(IMenuRegistry menus, string menuId, (string Command, string Group, ContextExpression? When)[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var (command, group, when) = items[i];
            _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(menuId, command, group, i, when: when)));
        }
    }

    private static ContextExpression When(string expression) => ContextExpression.Parse(expression);
}
