using System.Collections.ObjectModel;
using System.ComponentModel;
using CodeEditor.Core.Context;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.Editors;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// The panel change list: non-empty groups in order and the selected row (file or group). The selection is the target
/// of argument-less commands while the user works with the list; once focus returns to the editor text, the active file
/// is the target again, as in VS Code. Selection context keys drive context menu visibility.
/// </summary>
public sealed partial class GitChangesViewModel : ObservableObject, IDisposable
{
    private readonly GitRepository _repository;
    private readonly IContextKeyService _context;
    private readonly GitChangeGroupViewModel[] _all;

    public GitChangesViewModel(GitRepository repository, IContextKeyService context)
    {
        _repository = repository;
        _context = context;
        _all =
        [
            new GitChangeGroupViewModel(GitChangeGroup.Merge, Select),
            new GitChangeGroupViewModel(GitChangeGroup.Staged, Select),
            new GitChangeGroupViewModel(GitChangeGroup.Changes, Select),
        ];
        _repository.PropertyChanged += OnRepositoryChanged;
        _context.Changed += OnContextChanged;
        Update(_repository.Status);
    }

    /// <summary>Non-empty groups in display order.</summary>
    public ObservableCollection<GitChangeGroupViewModel> Groups { get; } = [];

    public bool HasChanges => Groups.Count > 0;

    /// <summary>Selected row: <see cref="GitChangeItemViewModel"/> or <see cref="GitChangeGroupViewModel"/>.</summary>
    [ObservableProperty]
    public partial object? Selected { get; private set; }

    /// <summary>The selection is the command target: the list had focus more recently than the editor text.</summary>
    public bool IsSelectionContext { get; private set; }

    public GitChangeGroupViewModel Group(GitChangeGroup group) => _all[(int)group];

    /// <summary>Keyboard focus in the list: <c>Enter</c> opens changes, the selection becomes the command target.</summary>
    public void SetFocused(bool isFocused)
    {
        _context.Set(GitContextKeys.ChangesFocus, isFocused);
        if (isFocused)
        {
            IsSelectionContext = true;
        }
    }

    public void Dispose()
    {
        _repository.PropertyChanged -= OnRepositoryChanged;
        _context.Changed -= OnContextChanged;
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitRepository.Status))
        {
            Update(_repository.Status);
        }
    }

    // Back in the editor text: argument-less commands target the active file again.
    private void OnContextChanged(object? sender, ContextKeyChangedEventArgs e)
    {
        if (e.Key == EditorContextKeys.TextFocus && _context.GetValue(e.Key) is true)
        {
            IsSelectionContext = false;
        }
    }

    private void Update(GitStatus status)
    {
        var position = 0;
        foreach (var group in _all)
        {
            group.Update([.. status.In(group.Group).OrderBy(change => change.Path, GitPathComparer.Instance)]);
            var shown = Groups.Contains(group);
            if (group.Count > 0 && !shown)
            {
                Groups.Insert(position, group);
            }
            else if (group.Count == 0 && shown)
            {
                Groups.Remove(group);
            }

            position += group.Count > 0 ? 1 : 0;
        }

        if (Selected is not null && !IsShown(Selected))
        {
            Select(null);
        }

        OnPropertyChanged(nameof(HasChanges));
    }

    private bool IsShown(object selected) => selected switch
    {
        GitChangeGroupViewModel group => Groups.Contains(group),
        GitChangeItemViewModel item => Groups.Any(group => group.Items.Contains(item)),
        _ => false,
    };

    private void Select(object? selected)
    {
        Selected = selected;
        var (group, isGroup, deleted) = selected switch
        {
            GitChangeItemViewModel item => (item.Change.Group, false, item.Change.IsDeleted),
            GitChangeGroupViewModel header => (header.Group, true, false),
            _ => (GitChangeGroup.Changes, false, false),
        };
        _context.Set(GitContextKeys.ResourceGroup, selected is null ? null : GitContextKeys.GroupValue(group));
        _context.Set(GitContextKeys.ResourceIsGroup, isGroup);
        _context.Set(GitContextKeys.ResourceDeleted, deleted);
    }
}
