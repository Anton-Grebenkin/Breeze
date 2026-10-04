using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Search.Resources;
using CodeEditor.Modules.Search.Services.Matching;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Code names from the user's message and where they occur, as <c>&lt;context&gt;</c> lines (ADR 0012).
/// Models always start by searching for names from the question; this does it up front, without the model.
/// Files are listed per name, and names absent from the code (e.g. an API operation name built at runtime) are
/// listed separately: a combined "files mentioning all names" list misled the model to a single literal hit.
/// One alternation search capped by <see cref="Timeout"/>; on timeout "not found" is not claimed.
/// </summary>
public sealed class RelatedCodeContext(TextSearchService search, IDocumentService documents, IUiDispatcher dispatcher) : IAgentContextProvider
{
    public const int MaxFilesPerName = 3;

    /// <summary>The message is sent with whatever was found by then.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>Only the most frequent files are shown, so there is no point collecting more.</summary>
    private const int MaxCollected = 200;

    public async ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var identifiers = MentionedIdentifiers.Find(request.Text);
        if (identifiers.Count == 0)
        {
            return [];
        }

        var (files, complete) = await FindAsync(identifiers, cancellationToken);
        var byName = identifiers.ToDictionary(name => name, _ => new List<(string Path, int Count)>(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var group in file.Matches.GroupBy(MatchedText))
            {
                if (byName.TryGetValue(group.Key, out var entries))
                {
                    entries.Add((file.RelativePath, group.Count()));
                }
            }
        }

        var lines = new List<string>();
        var found = byName.Where(pair => pair.Value.Count > 0).Select(pair => Describe(pair.Key, pair.Value)).ToList();
        if (found.Count > 0)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.RelatedFound, string.Join("; ", found)));
        }

        var missing = byName.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key).ToList();
        if (complete && missing.Count > 0)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.RelatedMissing, string.Join(", ", missing)));
        }

        return lines;
    }

    // The search is case-sensitive, so the matched text is exactly one of the names.
    private static string MatchedText(SearchMatch match) =>
        match.PreviewStart + match.PreviewLength <= match.Preview.Length ? match.Preview.Substring(match.PreviewStart, match.PreviewLength) : string.Empty;

    private static string Describe(string name, List<(string Path, int Count)> files)
    {
        var shown = files.OrderByDescending(file => file.Count).ThenBy(file => file.Path, StringComparer.Ordinal).Take(MaxFilesPerName)
            .Select(file => string.Create(CultureInfo.InvariantCulture, $"{file.Path} ({file.Count})"));
        var more = files.Count > MaxFilesPerName ? string.Format(CultureInfo.CurrentCulture, Strings.RelatedFilesMore, files.Count - MaxFilesPerName) : string.Empty;
        return $"{name} — {string.Join(", ", shown)}{more}";
    }

    private async Task<(List<FileSearchResult> Files, bool Complete)> FindAsync(IReadOnlyList<string> identifiers, CancellationToken cancellationToken)
    {
        // Longest first: "v.category_record_id" must not match as "category_record_id".
        var pattern = string.Join('|', identifiers.OrderByDescending(name => name.Length).Select(Regex.Escape));
        var options = new TextSearchOptions(pattern) { UseRegex = true, MatchCase = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var files = new List<FileSearchResult>();
        try
        {
            var unsaved = await UnsavedTexts.SnapshotAsync(documents, dispatcher);
            await foreach (var file in search.SearchAsync(options, unsaved, timeout.Token).WithCancellation(timeout.Token))
            {
                if (!SensitivePaths.IsSecret(file.RelativePath) && !SensitivePaths.IsAgentData(file.RelativePath))
                {
                    files.Add(file);
                }

                if (files.Count == MaxCollected)
                {
                    return (files, false);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out: partial results are still useful, but missing names can't be reported.
            return (files, false);
        }

        return (files, true);
    }
}
