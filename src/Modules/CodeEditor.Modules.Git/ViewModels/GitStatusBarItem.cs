using System.ComponentModel;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Shell.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// Branch in the status bar, as in VS Code: "main* ↑1 ↓2" means changes, ahead and behind the upstream. A click opens
/// the branch picker (<see cref="GitCommandIds.Checkout"/>). Hidden outside a repository.
/// </summary>
public sealed class GitStatusBarItem : IDisposable
{
    public const string Id = "git.branch";

    // Left of the caret position (editor items start at 10).
    private const int Order = 0;

    private readonly GitRepository _repository;
    private readonly StatusBarItemViewModel _item = new(Id, Order);
    private readonly IDisposable _registration;

    public GitStatusBarItem(GitRepository repository, StatusBarViewModel statusBar, ICommandService commands)
    {
        ArgumentNullException.ThrowIfNull(statusBar);
        _repository = repository;
        _item.Command = new AsyncRelayCommand(async () => await commands.ExecuteAsync(GitCommandIds.Checkout));
        _registration = statusBar.Add(_item);
        _repository.PropertyChanged += OnRepositoryChanged;
        Update();
    }

    public StatusBarItemViewModel Item => _item;

    public void Dispose()
    {
        _repository.PropertyChanged -= OnRepositoryChanged;
        _registration.Dispose();
    }

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GitRepository.Status) or nameof(GitRepository.State))
        {
            Update();
        }
    }

    private void Update()
    {
        _item.IsVisible = _repository.IsReady;
        _item.Text = GitBranchLabel.Text(_repository.Status);
        _item.ToolTip = GitBranchLabel.ToolTip(_repository.Status.Head);
    }
}
