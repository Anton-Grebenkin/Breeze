using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Search.Services.Matching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Search.Services;

/// <summary>
/// Searches indexed files. Files are read and matched in parallel; results stream through a
/// <see cref="Channel{T}"/> as found, so the first matches show before the scan ends. Binary and oversized files
/// are skipped; files modified in the editor are searched by their unsaved text.
/// </summary>
public sealed partial class TextSearchService(
    IFileIndex index,
    IFileSystem fileSystem,
    IOptionsMonitor<SearchSettings> settings,
    ILogger<TextSearchService> logger)
{
    /// <summary>Larger files are skipped: they are almost always data or binaries.</summary>
    public const long MaxFileSize = 16L * 1024 * 1024;

    public const int MaxMatchesPerFile = 1_000;

    /// <param name="unsavedTexts">Path → text of open modified documents, snapshotted on the UI thread.</param>
    /// <exception cref="ArgumentException">Invalid regex.</exception>
    public IAsyncEnumerable<FileSearchResult> SearchAsync(
        TextSearchOptions options,
        IReadOnlyDictionary<string, string> unsavedTexts,
        CancellationToken cancellationToken)
    {
        // Throw a regex error eagerly, before enumeration: it is shown under the input box.
        var matcher = TextMatcher.Create(options);
        return RunAsync(options, matcher, unsavedTexts, cancellationToken);
    }

    private async IAsyncEnumerable<FileSearchResult> RunAsync(
        TextSearchOptions options,
        TextMatcher matcher,
        IReadOnlyDictionary<string, string> unsavedTexts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await index.WhenReady.WaitAsync(cancellationToken).ConfigureAwait(false);

        var include = GlobFilter.Parse(options.Include);
        // Globs from the Exclude box and from the search.exclude setting.
        var exclude = GlobFilter.Create([
            .. (options.Exclude ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            .. settings.CurrentValue.Exclude.Where(pair => pair.Value).Select(pair => pair.Key),
        ]);
        var files = index.Files
            .Where(file => (include.IsEmpty || include.Matches(file.RelativePath)) && !exclude.Matches(file.RelativePath) && (options.Filter?.Invoke(file.RelativePath) ?? true))
            .ToArray();

        // The consumer may stop early (result limit); the file scan stops with it.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var channel = Channel.CreateUnbounded<FileSearchResult>(new UnboundedChannelOptions { SingleReader = true });
        var producer = ProduceAsync(files, matcher, options.PreviewLead, unsavedTexts, channel.Writer, stop.Token);
        try
        {
            await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return result;
            }
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            await producer.ConfigureAwait(false);
        }
    }

    private async Task ProduceAsync(
        IndexedFile[] files,
        TextMatcher matcher,
        int previewLead,
        IReadOnlyDictionary<string, string> unsavedTexts,
        ChannelWriter<FileSearchResult> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount };
            await Parallel.ForEachAsync(files, parallel, async (file, token) =>
            {
                if (SearchFile(file, matcher, previewLead, unsavedTexts) is { } result)
                {
                    await writer.WriteAsync(result, token).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    private FileSearchResult? SearchFile(IndexedFile file, TextMatcher matcher, int previewLead, IReadOnlyDictionary<string, string> unsavedTexts)
    {
        try
        {
            if (ReadText(file.FullPath, unsavedTexts) is not { } text)
            {
                return null;
            }

            var matches = MatchCollector.Collect(text, matcher, MaxMatchesPerFile, previewLead);
            return matches.Count > 0 ? new FileSearchResult(file.FullPath, file.RelativePath, matches) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Deleted or locked during the search: skip it, as VS Code does.
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            LogRegexTimeout(logger, file.RelativePath);
            return null;
        }
    }

    private string? ReadText(string path, IReadOnlyDictionary<string, string> unsavedTexts)
    {
        if (unsavedTexts.TryGetValue(path, out var unsaved))
        {
            return unsaved;
        }

        if (fileSystem.GetFileLength(path) > MaxFileSize)
        {
            return null;
        }

        return TextFileCodec.Decode(fileSystem.ReadAllBytes(path))?.Text;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Search: regular expression timed out in {Path}")]
    private static partial void LogRegexTimeout(ILogger logger, string path);
}
