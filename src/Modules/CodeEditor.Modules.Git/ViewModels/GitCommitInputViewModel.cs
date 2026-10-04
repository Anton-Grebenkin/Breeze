using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Context;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Shell.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// Commit message box and commit, as in VS Code: the index is committed; if it is empty but there are changes, all
/// changes including new files are committed after confirmation. The box is cleared after a commit.
/// </summary>
public sealed partial class GitCommitInputViewModel : ObservableObject, IDisposable
{
    private readonly GitRepository _repository;
    private readonly GitActions _actions;
    private readonly IDialogService _dialogs;
    private readonly IContextKeyService _context;

    public GitCommitInputViewModel(GitRepository repository, GitActions actions, IDialogService dialogs, IContextKeyService context)
    {
        _repository = repository;
        _actions = actions;
        _dialogs = dialogs;
        _context = context;
        _repository.PropertyChanged += OnRepositoryChanged;
    }

    /// <summary>Moves the caret into the box: shown via <c>Ctrl+Shift+G</c>, or a message is required.</summary>
    public event EventHandler? FocusRequested;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>Placeholder of the empty box: "Message (Ctrl+Enter to commit on 'main')".</summary>
    public string Placeholder => _repository.Status.Head.Branch is { } branch
        ? string.Format(CultureInfo.CurrentCulture, Strings.CommitPlaceholder, branch)
        : Strings.CommitPlaceholderNoBranch;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Focus in the box enables <c>Ctrl+Enter</c> to commit.</summary>
    public void SetFocused(bool isFocused) => _context.Set(GitContextKeys.CommitInputFocus, isFocused);

    /// <returns><c>true</c> when a commit was created.</returns>
    public async Task<bool> CommitAsync()
    {
        if (!_repository.IsReady)
        {
            _repository.ReportError(Strings.NoRepository);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            _repository.ReportError(Strings.CommitMessageRequired);
            RequestFocus();
            return false;
        }

        var status = _repository.Status;
        var stageAll = !status.HasStaged;
        if (stageAll && !status.HasUnstaged)
        {
            _repository.Notify(Strings.NothingToCommit);
            return false;
        }

        if (stageAll && !_dialogs.Confirm(Strings.CommitAllQuestion, Strings.CommitAllDetail, Strings.CommitAllConfirm))
        {
            return false;
        }

        if (!await _actions.CommitAsync(Message.Trim(), stageAll))
        {
            return false;
        }

        Message = string.Empty;
        return true;
    }

    public void Dispose() => _repository.PropertyChanged -= OnRepositoryChanged;

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitRepository.Status))
        {
            OnPropertyChanged(nameof(Placeholder));
        }
    }
}
