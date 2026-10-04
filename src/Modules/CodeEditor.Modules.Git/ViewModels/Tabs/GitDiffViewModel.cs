using System.Collections;
using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Modules.Git.Resources;
using CodeEditor.Modules.Git.Services;
using CodeEditor.Modules.Git.Services.Cli;
using CodeEditor.Modules.Git.Services.Parsing;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// Read-only file diff: rows with old and new line numbers, hunk headers and notes. Working tree and index diffs reload
/// when repository state refreshes; unchanged rows are kept, so scrolling is preserved. Loading runs in the background:
/// a new load cancels the previous one, closing the tab cancels the current one.
/// </summary>
public sealed partial class GitDiffViewModel : ObservableObject, IDisposable
{
    private readonly GitDiffSource _source;
    private readonly ICommandService _commands;
    private readonly ISystemShell _shell;
    private readonly GitRepository? _follow;
    private CancellationTokenSource? _loading;

    /// <param name="follow">Repository to follow; <c>null</c> for a diff that never changes (a commit).</param>
    public GitDiffViewModel(GitDiffSource source, ICommandService commands, ISystemShell shell, GitRepository? follow = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _commands = commands;
        _shell = shell;
        _follow = follow;
        if (_follow is not null)
        {
            _follow.StatusChanged += OnStatusChanged;
        }

        Loaded = LoadAsync();
    }

    public string Title => _source.Title;

    /// <summary>Path relative to the repository root.</summary>
    public string Path => _source.Path;

    public bool CanOpenFile => _source.FilePath is not null;

    /// <summary>The current load; tests await it.</summary>
    public Task Loaded { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<GitDiffRow> Rows { get; private set; } = [];

    /// <summary>"Loading…", "No changes" or a git error; <c>null</c> when rows are shown.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>"+12 −3": lines added and removed.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    public void Dispose()
    {
        if (_follow is not null)
        {
            _follow.StatusChanged -= OnStatusChanged;
        }

        _loading?.Cancel();
        _loading?.Dispose();
        _loading = null;
    }

    [RelayCommand]
    private Task RefreshAsync() => Loaded = LoadAsync();

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync() =>
        await _commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(_source.FilePath!));

    /// <summary>Copies the selected rows in diff order, without "+" and "-" markers.</summary>
    [RelayCommand]
    private void Copy(IList? selected)
    {
        if (selected is not { Count: > 0 })
        {
            return;
        }

        var chosen = new HashSet<object>(selected.Cast<object>(), ReferenceEqualityComparer.Instance);
        _shell.CopyToClipboard(string.Join(Environment.NewLine, Rows.Where(chosen.Contains).Select(row => row.Text)));
    }

    private void OnStatusChanged(object? sender, EventArgs e) => Loaded = LoadAsync();

    private async Task LoadAsync()
    {
        _loading?.Cancel();
        _loading?.Dispose();
        _loading = new CancellationTokenSource();
        var token = _loading.Token;
        if (Rows.Count == 0)
        {
            Message = Strings.Loading;
        }

        try
        {
            var rows = await _source.Load(token);
            if (!token.IsCancellationRequested)
            {
                Apply(rows);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab was closed or a new load started.
        }
        catch (GitException exception)
        {
            (Rows, Summary, Message) = ([], string.Empty, exception.Message);
        }
    }

    private void Apply(IReadOnlyList<GitDiffRow> rows)
    {
        Message = rows.Count == 0 ? Strings.NoChanges : null;
        if (rows.SequenceEqual(Rows))
        {
            return;
        }

        Rows = rows;
        var (added, removed) = CountChanges(rows);
        Summary = string.Create(CultureInfo.InvariantCulture, $"+{added} −{removed}");
    }

    // A single pass: a diff can have up to GitCli.MaxLines rows.
    private static (int Added, int Removed) CountChanges(IReadOnlyList<GitDiffRow> rows)
    {
        var (added, removed) = (0, 0);
        foreach (var row in rows)
        {
            added += row.Kind == GitDiffRowKind.Added ? 1 : 0;
            removed += row.Kind == GitDiffRowKind.Removed ? 1 : 0;
        }

        return (added, removed);
    }
}
