using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Modules.Git.ViewModels;
using CodeEditor.Modules.Git.ViewModels.Tabs;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;

namespace CodeEditor.Modules.Git.Commands;

/// <summary>
/// File commands: stage, unstage, discard (with confirmation), open file or changes. The target is the command argument
/// (a change, panel row or group); without one it is the selected row while the list is in use, otherwise the active
/// editor's file, as in VS Code.
/// </summary>
public sealed class GitChangeHandlers(
    GitRepository repository,
    GitActions actions,
    GitChangesViewModel changes,
    GitEditorTabs tabs,
    IDialogService dialogs,
    ICommandService commands,
    EditorAreaViewModel editors)
{
    public Task StageAsync(object? argument) =>
        Targets(argument, change => change.Group != GitChangeGroup.Staged) is { Count: > 0 } targets
            ? actions.StageAsync(Paths(targets))
            : Report(Strings.NothingToStage);

    public Task UnstageAsync(object? argument) =>
        Targets(argument, change => change.Group == GitChangeGroup.Staged) is { Count: > 0 } targets
            ? actions.UnstageAsync(Paths(targets))
            : Report(Strings.NothingToUnstage);

    /// <summary>Stages a group: the argument, the selected group or "Changes".</summary>
    public Task StageAllAsync(object? argument)
    {
        var group = argument switch
        {
            GitChangeGroup value => value,
            GitChangeGroupViewModel header => header.Group,
            _ when changes.IsSelectionContext && changes.Selected is GitChangeGroupViewModel header => header.Group,
            _ => GitChangeGroup.Changes,
        };
        group = group == GitChangeGroup.Staged ? GitChangeGroup.Changes : group;
        return repository.Status.In(group).Any() ? actions.StageGroupAsync(group) : Report(Strings.NothingToStage);
    }

    public Task UnstageAllAsync() =>
        repository.Status.HasStaged ? actions.UnstageAllAsync() : Report(Strings.NothingToUnstage);

    /// <summary>Discards working tree changes after confirmation; untracked files go to the recycle bin.</summary>
    public Task DiscardAsync(object? argument)
    {
        var targets = Targets(argument, change => change.Group == GitChangeGroup.Changes);
        if (targets.Count == 0)
        {
            return Report(Strings.NothingToDiscard);
        }

        return Confirm(targets) ? actions.DiscardAsync(targets) : Task.CompletedTask;
    }

    /// <param name="preview">Open as a preview tab (single click in the panel).</param>
    public Task OpenChangesAsync(object? argument, bool preview)
    {
        if (Single(argument) is not { } change)
        {
            return Report(Strings.NoChangesInFile);
        }

        tabs.OpenChanges(change, preview);
        return Task.CompletedTask;
    }

    public async Task OpenFileAsync(object? argument)
    {
        if (Single(argument) is not { } change)
        {
            await Report(Strings.NoChangesInFile);
        }
        else if (change.IsDeleted || repository.FullPath(change.Path) is not { } path)
        {
            await Report(Strings.FileIsDeleted);
        }
        else
        {
            await commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        }
    }

    private IReadOnlyList<GitFileChange> Targets(object? argument, Func<GitFileChange, bool> applies)
    {
        IEnumerable<GitFileChange> candidates = argument switch
        {
            GitFileChange change => [change],
            GitChangeItemViewModel item => [item.Change],
            GitChangeGroupViewModel group => group.Changes,
            _ when changes.IsSelectionContext && changes.Selected is GitChangeItemViewModel item => [item.Change],
            _ when changes.IsSelectionContext && changes.Selected is GitChangeGroupViewModel group => group.Changes,
            _ => ActiveFileChanges(),
        };
        return [.. candidates.Where(applies)];
    }

    // For the active editor's file, working tree changes win over staged ones, as in VS Code.
    private GitFileChange? Single(object? argument) => argument switch
    {
        GitFileChange change => change,
        GitChangeItemViewModel item => item.Change,
        _ when changes.IsSelectionContext && changes.Selected is GitChangeItemViewModel item => item.Change,
        _ when changes.IsSelectionContext && changes.Selected is GitChangeGroupViewModel => null,
        _ => ActiveFileChanges().OrderByDescending(change => change.Group).FirstOrDefault(),
    };

    private IEnumerable<GitFileChange> ActiveFileChanges() =>
        editors.Active?.FilePath is { } path && repository.RelativePath(path) is { } relative
            ? repository.Status.Changes.Where(change => string.Equals(change.Path, relative, StringComparison.OrdinalIgnoreCase))
            : [];

    private bool Confirm(IReadOnlyList<GitFileChange> targets)
    {
        if (targets is [{ IsUntracked: true } single])
        {
            return dialogs.Confirm(Format(Strings.DeleteUntrackedQuestion, single.Path), Strings.DeleteUntrackedDetail, Strings.DeleteUntrackedConfirm);
        }

        var question = targets.Count == 1
            ? Format(Strings.DiscardQuestion, targets[0].Path)
            : Format(Strings.DiscardManyQuestion, Plural.Format(targets.Count, Strings.DiscardFileForms));
        return dialogs.Confirm(question, Strings.DiscardDetail, Strings.DiscardConfirm);
    }

    private Task Report(string message)
    {
        repository.Notify(message);
        return Task.CompletedTask;
    }

    private static List<string> Paths(IEnumerable<GitFileChange> changes) =>
        [.. changes.Select(change => change.Path).Distinct(StringComparer.Ordinal)];

    private static string Format(string format, string argument) => string.Format(CultureInfo.CurrentCulture, format, argument);
}
