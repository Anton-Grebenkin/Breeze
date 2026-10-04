using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Keybindings;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Modules.Git.ViewModels.Tabs;

namespace CodeEditor.Modules.Git.Commands;

/// <summary>
/// Registers the git commands (<see cref="GitCommandIds"/>) and their key bindings: <c>Ctrl+Enter</c> in the message box
/// commits, <c>Enter</c> in the change list opens the file's changes.
/// </summary>
public sealed class GitPanelCommands(
    GitRepository repository,
    GitActions actions,
    GitChangeHandlers changes,
    GitCommitInputViewModel commit,
    GitBranchPicker branches,
    GitEditorTabs tabs) : IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IKeybindingRegistry keybindings)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(keybindings);
        var ready = When($"{GitContextKeys.State} == '{GitContextKeys.Ready}'");
        var canInit = When($"{IWorkspace.OpenContextKey} && {GitContextKeys.State} == '{GitContextKeys.NotRepository}'");

        Add(commands, GitCommandIds.Commit, Strings.CommandCommit, _ => commit.CommitAsync(), ready);
        Add(commands, GitCommandIds.Stage, Strings.CommandStage, changes.StageAsync, ready);
        Add(commands, GitCommandIds.Unstage, Strings.CommandUnstage, changes.UnstageAsync, ready);
        Add(commands, GitCommandIds.StageAll, Strings.CommandStageAll, changes.StageAllAsync, ready);
        Add(commands, GitCommandIds.UnstageAll, Strings.CommandUnstageAll, _ => changes.UnstageAllAsync(), ready);
        Add(commands, GitCommandIds.Discard, Strings.CommandDiscard, changes.DiscardAsync, ready);
        Add(commands, GitCommandIds.OpenChanges, Strings.CommandOpenChanges, argument => changes.OpenChangesAsync(argument, preview: false), ready);
        Add(commands, GitCommandIds.OpenFile, Strings.CommandOpenFile, changes.OpenFileAsync, ready);
        Add(commands, GitCommandIds.Refresh, Strings.CommandRefresh, _ => RefreshAsync(), When(IWorkspace.OpenContextKey));
        Add(commands, GitCommandIds.Checkout, Strings.CommandCheckout, _ => branches.PickAsync(), ready);
        Add(commands, GitCommandIds.CreateBranch, Strings.CommandCreateBranch, _ => Run(branches.ShowCreate), ready);
        Add(commands, GitCommandIds.Pull, Strings.CommandPull, _ => actions.PullAsync(), ready);
        Add(commands, GitCommandIds.Push, Strings.CommandPush, _ => actions.PushAsync(), ready);
        Add(commands, GitCommandIds.Fetch, Strings.CommandFetch, _ => actions.FetchAsync(), ready);
        Add(commands, GitCommandIds.ShowHistory, Strings.CommandShowHistory, _ => Run(tabs.OpenHistory), ready);
        Add(commands, GitCommandIds.Init, Strings.CommandInit, _ => actions.InitAsync(), canInit);

        Bind(keybindings, "Ctrl+Enter", GitCommandIds.Commit, When(GitContextKeys.CommitInputFocus));
        Bind(keybindings, "Enter", GitCommandIds.OpenChanges, When($"{GitContextKeys.ChangesFocus} && !{GitContextKeys.ResourceIsGroup}"));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    // A manual refresh also clears the previous error: it may be gone by now.
    private Task RefreshAsync()
    {
        repository.ClearError();
        return repository.RefreshAsync();
    }

    private void Add(ICommandRegistry commands, string id, string title, Func<object?, Task> handler, ContextExpression when) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (argument, _) => await handler(argument), Strings.ModuleName, when)));

    private void Bind(IKeybindingRegistry keybindings, string keys, string commandId, ContextExpression when) =>
        _registrations.Add(keybindings.Register(new KeybindingDefinition(KeySequence.Parse(keys), commandId, when)));

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    private static ContextExpression When(string expression) => ContextExpression.Parse(expression);
}
