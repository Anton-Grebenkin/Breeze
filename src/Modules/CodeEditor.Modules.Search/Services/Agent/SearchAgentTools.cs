using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services.Matching;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Agent tool <c>search_text</c>: the same search as the <c>Ctrl+Shift+F</c> panel, including unsaved text.
/// Modes as in Claude Code's Grep (matches with context, files only, counts), paged. Secret files are excluded.
/// The scope (<c>path</c>, <c>include</c>) is validated first (<see cref="SearchScope"/>): an empty scope reports
/// "nothing searched" with similar paths rather than "nothing found". An empty result is retried in the other mode
/// (regex ↔ literal) with a note, as Copilot does.
/// </summary>
public sealed class SearchAgentTools(
    TextSearchService search,
    IFileIndex index,
    IWorkspace workspace,
    IDocumentService documents,
    IFileSystem fileSystem,
    IUiDispatcher dispatcher) : IAgentToolProvider
{
    public const int DefaultMaxResults = 50;
    public const int MaxResultsLimit = 200;
    public const string SearchTextName = "search_text";
    public const int MaxContextLines = 5;

    // The model needs the whole line, not one trimmed for a narrow panel.
    private const int AgentPreviewLead = 200;

    // Characters that make a query mean different things as a regex and as literal text.
    private static readonly SearchValues<char> RegexCharacters = SearchValues.Create(@"\^$.|?*+()[]{}");

    private readonly SearchHints _hints = new();

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(SearchTextAsync, SearchTextName,
            "Searches text in workspace files (unsaved editor changes included). Output is grouped by file: 'path' then 'line: text'; with contextLines, neighbour lines are 'line- text'. " +
            "mode 'files' lists only matching files with counts, 'count' gives totals — use them to survey before reading. " +
            "The query is a .NET regular expression by default, like ripgrep: escape special characters or pass isRegex=false for literal text. " +
            "Prefer one regex with alternation ('Foo|Bar') over several searches. " +
            "path limits the search to a folder or a file (use real folder names from <workspace> or list_dir); include filters files by globs with braces ('*.{cs,sql}'). Page with offset.")),
    ];

    private async Task<string> SearchTextAsync(
        [Description(".NET regular expression (default) or literal text to find.")] string query,
        [Description("false — search the query as literal text.")] bool isRegex = true,
        [Description("Case-sensitive search.")] bool matchCase = false,
        [Description("Optional folder or file to search in, relative to the workspace root, like 'src/Acme.Catalog' or 'src/App/Program.cs'.")] string? path = null,
        [Description("Optional comma-separated globs of files to search, like '*.cs' or '**/*.{cs,sql}'.")] string? include = null,
        [Description("'content' (default): matching lines; 'files': matching files with counts; 'count': totals.")] string mode = SearchOutput.Content,
        [Description("Lines of context around each match, 0–5 (content mode).")] int contextLines = 0,
        [Description("Maximum matches (content) or files (files, count) to return, 1–200.")] int maxResults = DefaultMaxResults,
        [Description("Skip this many results — for the next page.")] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
        {
            throw new AgentToolException(Strings.ToolEmptyQuery);
        }

        await index.WhenReady.WaitAsync(cancellationToken);
        var scope = SearchScope.Create(index.Files, RelativePath(path), path, include);
        if (scope.EmptyScopeMessage(index.Files, include) is { } nothingSearched)
        {
            return nothingSearched;
        }

        var unsaved = await UnsavedTexts.SnapshotAsync(documents, dispatcher);
        // Agent data (chats, memory, old logs) is skipped; saved output only when the model searches it explicitly.
        var inOutputs = (include + path)?.Contains(SensitivePaths.AgentOutputsFolder, StringComparison.OrdinalIgnoreCase) == true;
        var options = new TextSearchOptions(query) { UseRegex = isRegex, MatchCase = matchCase, Filter = scope.Contains, PreviewLead = AgentPreviewLead };
        bool IsVisible(string file) => !SensitivePaths.IsAgentData(file) || (inOutputs && SensitivePaths.IsAgentOutput(file));
        var (files, note) = await CollectAsync(options, IsVisible, unsaved, cancellationToken);
        if (files.Count == 0 && note.Length == 0 && OtherMode(options) is { } other
            && await CollectAsync(other, IsVisible, unsaved, cancellationToken) is { Files.Count: > 0 } retry)
        {
            (files, note) = (retry.Files, (other.UseRegex ? Strings.ToolFoundAsRegex : Strings.ToolFoundAsText) + "\n");
        }

        var output = note + SearchOutput.Format(files, mode, Math.Max(0, offset), Math.Clamp(maxResults, 1, MaxResultsLimit), Math.Clamp(contextLines, 0, MaxContextLines),
            file => ReadLines(file, unsaved));
        return ToolOutput.Limit(output) + _hints.After(path ?? include, files.Count > 0);
    }

    // The model may pass a relative or full path; WorkspacePaths keeps it inside the workspace.
    private string? RelativePath(string? path) =>
        string.IsNullOrWhiteSpace(path) || path.Trim() is "." or "./"
            ? null
            : workspace.RelativePath(WorkspacePaths.Resolve(workspace, path.Trim()));

    // The other mode, if the query means something different there: it has regex characters and is a valid regex.
    private static TextSearchOptions? OtherMode(TextSearchOptions options)
    {
        if (options.Pattern.AsSpan().IndexOfAny(RegexCharacters) < 0)
        {
            return null;
        }

        return options.UseRegex ? options with { UseRegex = false } : IsRegex(options.Pattern) ? options with { UseRegex = true } : null;
    }

    private static bool IsRegex(string pattern)
    {
        try
        {
            _ = new Regex(pattern);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // Models write ripgrep-style queries; an invalid regex like "Run(" falls back to literal search with a note.
    private async Task<(List<FileSearchResult> Files, string Note)> CollectAsync(
        TextSearchOptions options, Func<string, bool> isVisible, Dictionary<string, string> unsaved, CancellationToken cancellationToken)
    {
        IAsyncEnumerable<FileSearchResult> results;
        var note = string.Empty;
        try
        {
            results = search.SearchAsync(options, unsaved, cancellationToken);
        }
        catch (ArgumentException exception) when (options.UseRegex)
        {
            results = search.SearchAsync(options with { UseRegex = false }, unsaved, cancellationToken);
            note = string.Format(CultureInfo.CurrentCulture, Strings.ToolRegexFallback, exception.InnerException?.Message ?? exception.Message) + "\n";
        }

        return (await CollectAsync(results, isVisible, cancellationToken), note);
    }

    private static async Task<List<FileSearchResult>> CollectAsync(IAsyncEnumerable<FileSearchResult> results, Func<string, bool> isVisible, CancellationToken cancellationToken)
    {
        var files = new List<FileSearchResult>();
        await foreach (var file in results.WithCancellation(cancellationToken))
        {
            if (!SensitivePaths.IsSecret(file.RelativePath) && isVisible(file.RelativePath))
            {
                files.Add(file);
            }
        }

        // The search runs in parallel: sort by path for stable output.
        files.Sort((left, right) => NaturalStringComparer.Instance.Compare(left.RelativePath, right.RelativePath));
        return files;
    }

    // Context lines come from unsaved editor text, otherwise from disk.
    private string[] ReadLines(FileSearchResult file, Dictionary<string, string> unsaved)
    {
        if (unsaved.TryGetValue(file.FullPath, out var text))
        {
            return text.Split('\n');
        }

        try
        {
            return TextFileCodec.Decode(fileSystem.ReadAllBytes(file.FullPath))?.Text.Split('\n') ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
