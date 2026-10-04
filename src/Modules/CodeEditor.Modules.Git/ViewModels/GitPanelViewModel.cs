using System.ComponentModel;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Git.Commands;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Git.ViewModels;

/// <summary>
/// Source Control panel, as in VS Code: branch and buttons (refresh, Pull, Push, "…"), message box and Commit, change
/// groups; without a repository, a hint and "Initialize Repository". Buttons, rows and menus run the same git commands
/// (<see cref="GitCommandIds"/>) as the palette and key bindings. Created on first show.
/// </summary>
public sealed partial class GitPanelViewModel : ObservableObject, IFocusableContent, IDisposable
{
    private static readonly string[] StateProperties =
    [
        nameof(IsReady), nameof(IsNotRepository), nameof(IsNoGit), nameof(IsNoFolder), nameof(IsLoading), nameof(CanAct),
        nameof(BranchText), nameof(BranchToolTip), nameof(HasNoChanges),
    ];

    private readonly ICommandService _commands;
    private readonly GitChangeHandlers _handlers;
    private readonly MenuViewModel _fileMenu;
    private readonly MenuViewModel _groupMenu;

    public GitPanelViewModel(
        GitRepository repository,
        GitChangesViewModel changes,
        GitCommitInputViewModel commit,
        GitChangeHandlers handlers,
        ICommandService commands,
        MenuViewModelFactory menus)
    {
        ArgumentNullException.ThrowIfNull(menus);
        Repository = repository;
        Changes = changes;
        Commit = commit;
        _handlers = handlers;
        _commands = commands;
        MoreMenu = menus.Create(GitMenus.Main);
        _fileMenu = menus.Create(GitMenus.File);
        _groupMenu = menus.Create(GitMenus.Group);
        Repository.PropertyChanged += OnRepositoryChanged;
        Changes.PropertyChanged += OnChangesChanged;
    }

    /// <summary>Moves the caret into the message box on <c>Ctrl+Shift+G</c>, as in VS Code.</summary>
    public event EventHandler? FocusRequested
    {
        add => Commit.FocusRequested += value;
        remove => Commit.FocusRequested -= value;
    }

    public GitRepository Repository { get; }

    public GitChangesViewModel Changes { get; }

    public GitCommitInputViewModel Commit { get; }

    /// <summary>The header "…" menu, same as "Git" in the menu bar.</summary>
    public MenuViewModel MoreMenu { get; }

    /// <summary>List context menu: the file or group menu, depending on the selected row.</summary>
    public MenuViewModel ContextMenu => Changes.Selected is GitChangeGroupViewModel ? _groupMenu : _fileMenu;

    public bool IsReady => Repository.State == GitRepositoryState.Ready;

    public bool IsNotRepository => Repository.State == GitRepositoryState.NotRepository;

    public bool IsNoGit => Repository.State == GitRepositoryState.NoGit;

    public bool IsNoFolder => Repository.State == GitRepositoryState.NoFolder;

    public bool IsLoading => Repository.State == GitRepositoryState.Unknown;

    /// <summary>An action can start: the repository is read and no other action is running.</summary>
    public bool CanAct => IsReady && !Repository.IsBusy;

    public bool HasNoChanges => IsReady && !Changes.HasChanges;

    public string BranchText => GitBranchLabel.Text(Repository.Status);

    public string BranchToolTip => GitBranchLabel.ToolTip(Repository.Status.Head);

    public void RequestFocus() => Commit.RequestFocus();

    /// <summary>The panel became visible (side bar, bottom or editor tab): state is read at once, not delayed.</summary>
    public void OnShown() => _ = Repository.RefreshAsync();

    public void Dispose()
    {
        Repository.PropertyChanged -= OnRepositoryChanged;
        Changes.PropertyChanged -= OnChangesChanged;
        MoreMenu.Dispose();
        _fileMenu.Dispose();
        _groupMenu.Dispose();
    }

    /// <summary>Panel button: runs a git command by id (Refresh, Pull, Push, Commit, branch picker).</summary>
    [RelayCommand]
    private async Task RunAsync(string? commandId)
    {
        if (commandId is not null)
        {
            await _commands.ExecuteAsync(commandId);
        }
    }

    [RelayCommand]
    private Task StageAsync(object? target) => ExecuteAsync(GitCommandIds.Stage, target);

    [RelayCommand]
    private Task UnstageAsync(object? target) => ExecuteAsync(GitCommandIds.Unstage, target);

    [RelayCommand]
    private Task DiscardAsync(object? target) => ExecuteAsync(GitCommandIds.Discard, target);

    [RelayCommand]
    private Task OpenFileAsync(object? target) => ExecuteAsync(GitCommandIds.OpenFile, target);

    /// <summary>Double click or <c>Enter</c>: a regular changes tab.</summary>
    [RelayCommand]
    private Task OpenChangesAsync(object? target) => ExecuteAsync(GitCommandIds.OpenChanges, target);

    /// <summary>Single click: a preview tab; focus stays in the list.</summary>
    [RelayCommand]
    private Task PreviewChangesAsync(object? target) => _handlers.OpenChangesAsync(Argument(target), preview: true);

    [RelayCommand]
    private Task StageGroupAsync(GitChangeGroupViewModel? group) =>
        group is null ? Task.CompletedTask : ExecuteAsync(GitCommandIds.StageAll, group.Group);

    [RelayCommand]
    private Task UnstageGroupAsync() => ExecuteAsync(GitCommandIds.UnstageAll, argument: null);

    [RelayCommand]
    private void DismissError() => Repository.ClearError();

    private async Task ExecuteAsync(string commandId, object? argument) => await _commands.ExecuteAsync(commandId, Argument(argument));

    // A list row is passed to commands as its change, so commands do not depend on row view models.
    private static object? Argument(object? target) => target is GitChangeItemViewModel item ? item.Change : target;

    private void OnRepositoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GitRepository.State) or nameof(GitRepository.Status) or nameof(GitRepository.Activity))
        {
            foreach (var property in StateProperties)
            {
                OnPropertyChanged(property);
            }
        }
    }

    private void OnChangesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GitChangesViewModel.Selected))
        {
            OnPropertyChanged(nameof(ContextMenu));
        }
        else if (e.PropertyName == nameof(GitChangesViewModel.HasChanges))
        {
            OnPropertyChanged(nameof(HasNoChanges));
        }
    }
}
