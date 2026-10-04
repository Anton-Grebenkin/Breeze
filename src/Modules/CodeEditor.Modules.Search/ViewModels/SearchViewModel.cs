using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services;
using CodeEditor.Modules.Search.Services.Matching;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Search.ViewModels;

/// <summary>
/// Search panel (<c>Ctrl+Shift+F</c>), as in VS Code: debounced search-as-you-type, case, whole word, regex and file
/// globs; results are grouped by file in path order and appear as they are found.
/// </summary>
public sealed partial class SearchViewModel : ObservableObject, IFocusableContent, IDisposable
{
    /// <summary>Beyond this the query needs refining; no more matches are shown.</summary>
    public const int MaxResults = 20_000;

    /// <summary>Context key for "search panel focused"; gates <c>Alt+C</c>, <c>Alt+W</c>, <c>Alt+R</c>.</summary>
    public const string FocusContextKey = "searchViewFocus";

    /// <summary>Debounce after typing, so the search doesn't run on every keystroke.</summary>
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

    private readonly TextSearchService _search;
    private readonly IDocumentService _documents;
    private readonly ICommandService _commands;
    private readonly IWorkspace _workspace;
    private readonly TimeProvider _time;
    private readonly IContextKeyService _context;
    private CancellationTokenSource? _run;

    public SearchViewModel(
        TextSearchService search,
        IDocumentService documents,
        ICommandService commands,
        IWorkspace workspace,
        TimeProvider time,
        IContextKeyService context)
    {
        _search = search;
        _documents = documents;
        _commands = commands;
        _workspace = workspace;
        _time = time;
        _context = context;
        _workspace.Changed += OnWorkspaceChanged;
    }

    public event EventHandler? FocusRequested;

    public ObservableCollection<SearchFileViewModel> Files { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    [ObservableProperty]
    public partial bool WholeWord { get; set; }

    [ObservableProperty]
    public partial bool UseRegex { get; set; }

    [ObservableProperty]
    public partial string Include { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Exclude { get; set; } = string.Empty;

    /// <summary>Glob fields are expanded (the "…" button).</summary>
    [ObservableProperty]
    public partial bool ShowFilters { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    /// <summary>"12 results in 3 files", "Nothing found", or empty before a search.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    /// <summary>Query error (invalid regex), shown under the input box.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool HasWorkspace => _workspace.Root is not null;

    public int ResultCount { get; private set; }

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void SetFocused(bool isFocused) => _context.Set(FocusContextKey, isFocused);

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _run?.Cancel();
        _run?.Dispose();
    }

    /// <summary><c>Enter</c> in the box: search immediately, without the debounce.</summary>
    [RelayCommand]
    public Task SearchAsync() => RunAsync(TimeSpan.Zero);

    [RelayCommand]
    public void Clear()
    {
        Query = string.Empty;
        Reset(string.Empty);
    }

    [RelayCommand]
    private void ToggleFilters() => ShowFilters = !ShowFilters;

    /// <summary>Collapses all files, or expands them if all are collapsed.</summary>
    [RelayCommand]
    private void ToggleCollapseAll()
    {
        var expand = Files.All(file => !file.IsExpanded);
        foreach (var file in Files)
        {
            file.IsExpanded = expand;
        }
    }

    /// <summary>Single click: preview tab with the match selected; focus stays in the list.</summary>
    [RelayCommand]
    private Task PreviewMatchAsync(SearchMatchViewModel? match) => OpenAsync(match, preview: true);

    /// <summary>Double click or <c>Enter</c>: regular tab, focus moves to the text.</summary>
    [RelayCommand]
    private Task OpenMatchAsync(SearchMatchViewModel? match) => OpenAsync(match, preview: false);

    private async Task OpenAsync(SearchMatchViewModel? match, bool preview)
    {
        if (match is null)
        {
            return;
        }

        await _commands.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(match.File.FullPath, preview));
        await _commands.ExecuteAsync(ShellCommandIds.EditorGoToLine, match.Location with { PreserveFocus = preview });
    }

    partial void OnQueryChanged(string value) => Restart();

    partial void OnMatchCaseChanged(bool value) => Restart();

    partial void OnWholeWordChanged(bool value) => Restart();

    partial void OnUseRegexChanged(bool value) => Restart();

    partial void OnIncludeChanged(string value) => Restart();

    partial void OnExcludeChanged(string value) => Restart();

    private void Restart() => _ = RunAsync(TypingDelay);

    // Another folder makes old results meaningless; the query is kept and re-run.
    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasWorkspace));
        _ = RunAsync(TimeSpan.Zero);
    }

    private async Task RunAsync(TimeSpan delay)
    {
        _run?.Cancel();
        _run?.Dispose();
        var run = _run = new CancellationTokenSource();

        if (string.IsNullOrEmpty(Query) || !HasWorkspace)
        {
            Reset(string.Empty);
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _time, run.Token);
            }

            await CollectAsync(CreateOptions(), run);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            // The query changed; the next search replaces the results.
        }
        finally
        {
            if (ReferenceEquals(run, _run))
            {
                IsSearching = false;
            }
        }
    }

    private async Task CollectAsync(TextSearchOptions options, CancellationTokenSource run)
    {
        IAsyncEnumerable<FileSearchResult> results;
        try
        {
            results = _search.SearchAsync(options, SnapshotUnsaved(), run.Token);
        }
        catch (ArgumentException exception)
        {
            Reset(string.Empty);
            Error = exception.Message;
            return;
        }

        Reset(string.Empty);
        IsSearching = true;
        await foreach (var result in results.WithCancellation(run.Token))
        {
            Insert(new SearchFileViewModel(result));
            ResultCount += result.Matches.Count;
            if (ResultCount >= MaxResults)
            {
                await run.CancelAsync();
                Summary = string.Format(CultureInfo.CurrentCulture, Strings.SummaryTruncated, FormatSummary());
                return;
            }
        }

        Summary = ResultCount == 0 ? Strings.NothingFoundHint : FormatSummary();
    }

    private TextSearchOptions CreateOptions() => new(Query)
    {
        MatchCase = MatchCase,
        WholeWord = WholeWord,
        UseRegex = UseRegex,
        Include = Include,
        Exclude = Exclude,
    };

    /// <summary>Modified documents are searched instead of their files on disk. Runs on the UI thread.</summary>
    private Dictionary<string, string> SnapshotUnsaved() =>
        _documents.Documents
            .Where(document => document.IsDirty)
            .ToDictionary(document => document.FilePath, document => document.Buffer.GetText(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Binary-search insert by path: results arrive from a parallel scan in arbitrary order.</summary>
    private void Insert(SearchFileViewModel file)
    {
        int low = 0, high = Files.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (NaturalStringComparer.Instance.Compare(Files[middle].RelativePath, file.RelativePath) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        Files.Insert(low, file);
    }

    private void Reset(string summary)
    {
        Files.Clear();
        ResultCount = 0;
        Error = null;
        Summary = summary;
    }

    private string FormatSummary() =>
        string.Format(CultureInfo.CurrentCulture, Strings.CountInFiles, Plural.Format(ResultCount, Strings.ResultForms), Plural.Format(Files.Count, Strings.InFileForms));
}
