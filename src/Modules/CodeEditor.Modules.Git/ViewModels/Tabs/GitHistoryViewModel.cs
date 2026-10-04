using System.Collections.ObjectModel;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Shell.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// History of the current branch in an editor tab: commits in pages of <see cref="GitReader.LogPageSize"/>; for the
/// selected one, its message, files and the selected file's diff. When HEAD moves (commit, branch switch) the list
/// reloads. Loads run in the background; a new load cancels the previous one, closing the tab cancels everything.
/// </summary>
public sealed partial class GitHistoryViewModel : ObservableObject, IDisposable
{
    private readonly GitRepository _repository;
    private readonly GitReader _reader;
    private readonly GitDiffFactory _diffs;
    private readonly ISystemShell _shell;
    private CancellationTokenSource _list = new();
    private CancellationTokenSource? _details;
    private string? _head;

    public GitHistoryViewModel(GitRepository repository, GitReader reader, GitDiffFactory diffs, ISystemShell shell)
    {
        _repository = repository;
        _reader = reader;
        _diffs = diffs;
        _shell = shell;
        _head = repository.Status.Head.Commit;
        _repository.StatusChanged += OnStatusChanged;
        Loaded = ReloadAsync();
    }

    public ObservableCollection<GitLogEntryViewModel> Commits { get; } = [];

    /// <summary>The latest list load; tests await it.</summary>
    public Task Loaded { get; private set; }

    /// <summary>Loading of the selected commit's files.</summary>
    public Task DetailsLoaded { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial GitLogEntryViewModel? SelectedCommit { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<GitCommitFileViewModel> Files { get; private set; } = [];

    [ObservableProperty]
    public partial GitCommitFileViewModel? SelectedFile { get; set; }

    /// <summary>Diff of the selected commit file.</summary>
    [ObservableProperty]
    public partial GitDiffViewModel? Diff { get; private set; }

    /// <summary>There are more commits beyond those shown.</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>Above the list: "Loading…", "No commits yet" or a git error.</summary>
    [ObservableProperty]
    public partial string? ListMessage { get; private set; }

    /// <summary>Above the details: a hint to select a commit, loading or an error.</summary>
    [ObservableProperty]
    public partial string? DetailsMessage { get; private set; } = Strings.SelectCommitHint;

    public void Dispose()
    {
        _repository.StatusChanged -= OnStatusChanged;
        _list.Cancel();
        _list.Dispose();
        _details?.Cancel();
        _details?.Dispose();
        Diff?.Dispose();
    }

    [RelayCommand]
    private Task RefreshAsync() => Loaded = ReloadAsync();

    [RelayCommand]
    private Task LoadMoreAsync() => Loaded = LoadPageAsync(Commits.Count, _list.Token);

    [RelayCommand]
    private void CopyHash()
    {
        if (SelectedCommit is { } entry)
        {
            _shell.CopyToClipboard(entry.Commit.Hash);
        }
    }

    partial void OnSelectedCommitChanged(GitLogEntryViewModel? value) => DetailsLoaded = LoadDetailsAsync(value);

    partial void OnSelectedFileChanged(GitCommitFileViewModel? value)
    {
        Diff?.Dispose();
        Diff = value is not null && SelectedCommit is { } entry ? _diffs.ForCommitFile(entry.Commit.Hash, value.File) : null;
    }

    // HEAD moved (a new commit or another branch): reload the list.
    private void OnStatusChanged(object? sender, EventArgs e)
    {
        var head = _repository.Status.Head.Commit;
        if (head != _head)
        {
            _head = head;
            Loaded = ReloadAsync();
        }
    }

    private async Task ReloadAsync()
    {
        _list.Cancel();
        _list.Dispose();
        _list = new CancellationTokenSource();
        Commits.Clear();
        (SelectedCommit, HasMore) = (null, false);
        await LoadPageAsync(0, _list.Token);
    }

    private async Task LoadPageAsync(int skip, CancellationToken token)
    {
        if (_repository.Location is not { } location)
        {
            ListMessage = Strings.NoRepository;
            return;
        }

        ListMessage = Commits.Count == 0 ? Strings.Loading : null;
        try
        {
            var page = await _reader.LogAsync(location.Root, skip, token);
            token.ThrowIfCancellationRequested();
            foreach (var commit in page)
            {
                Commits.Add(new GitLogEntryViewModel(commit));
            }

            HasMore = page.Count == GitReader.LogPageSize;
            ListMessage = Commits.Count == 0 ? Strings.HistoryEmpty : null;
            SelectedCommit ??= Commits.FirstOrDefault();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The list is reloading or the tab was closed.
        }
        catch (GitException exception)
        {
            ListMessage = _repository.Status.Head.IsInitial ? Strings.HistoryEmpty : exception.Message;
        }
    }

    private async Task LoadDetailsAsync(GitLogEntryViewModel? entry)
    {
        _details?.Cancel();
        _details?.Dispose();
        _details = new CancellationTokenSource();
        var token = _details.Token;
        (SelectedFile, Files) = (null, []);
        if (entry is null || _repository.Location is not { } location)
        {
            DetailsMessage = Strings.SelectCommitHint;
            return;
        }

        DetailsMessage = Strings.Loading;
        try
        {
            var files = await _reader.CommitFilesAsync(location.Root, entry.Commit.Hash, token);
            token.ThrowIfCancellationRequested();
            Files = [.. files.Select(file => new GitCommitFileViewModel(file))];
            DetailsMessage = null;
            SelectedFile = Files.FirstOrDefault();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Another commit was selected.
        }
        catch (GitException exception)
        {
            DetailsMessage = exception.Message;
        }
    }
}
