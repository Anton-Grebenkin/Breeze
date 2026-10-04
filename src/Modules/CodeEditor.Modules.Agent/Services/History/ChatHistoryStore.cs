using System.Text.Json;
using CodeEditor.Core.Files;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.History;

/// <summary>
/// Chats of the open folder in <c>.breeze/agent/</c>: a file per chat and an <c>index.json</c> for the list, since chat
/// files with the model session can be large and the list must be fast. A missing or corrupt index is rebuilt from the
/// files. The folder's <c>.gitignore</c> keeps history out of the repository.
/// </summary>
public sealed partial class ChatHistoryStore(IWorkspace workspace, IFileSystem fileSystem, ILogger<ChatHistoryStore> logger)
{
    public const string IndexFileName = "index.json";

    private const string FilePrefix = "chat-";
    private const string FileExtension = ".json";

    public string? Folder => AgentDataFolder.PathOf(workspace);

    /// <summary>Folder chats, most recently answered first.</summary>
    public IReadOnlyList<ChatSummary> List() =>
        [.. ReadIndex().OrderByDescending(static summary => summary.Updated)];

    public ChatTranscript? Load(string id) =>
        Folder is { } folder && fileSystem.FileExists(PathOf(folder, id)) ? Read(PathOf(folder, id)) : null;

    /// <summary>The folder's latest chat; corrupt files are skipped.</summary>
    public ChatTranscript? LoadLatest() =>
        List().Select(summary => Load(summary.Id)).FirstOrDefault(transcript => transcript is not null);

    public void Save(ChatTranscript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        if (Folder is not { } folder)
        {
            return;
        }

        try
        {
            AgentDataFolder.Ensure(workspace, fileSystem, folder);
            fileSystem.WriteAllBytesAtomic(PathOf(folder, transcript.Id),
                JsonSerializer.SerializeToUtf8Bytes(transcript, AgentJsonContext.Readable.ChatTranscript));
            WriteIndex(folder, [.. ReadIndex().Where(summary => summary.Id != transcript.Id), transcript.ToSummary()]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, folder);
        }
    }

    /// <summary>Moves the chat to the Windows recycle bin (so it can be restored) and removes it from the list.</summary>
    public void Delete(string id)
    {
        if (Folder is not { } folder)
        {
            return;
        }

        try
        {
            if (fileSystem.FileExists(PathOf(folder, id)))
            {
                fileSystem.DeleteToRecycleBin(PathOf(folder, id));
            }

            WriteIndex(folder, [.. ReadIndex().Where(summary => summary.Id != id)]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, folder);
        }
    }

    /// <summary>File names sort by time: <c>chat-20260927-153012-123</c>.</summary>
    public static string NewId(DateTimeOffset now) => now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);

    private static string PathOf(string folder, string id) => Path.Combine(folder, FilePrefix + id + FileExtension);

    private List<ChatSummary> ReadIndex()
    {
        if (Folder is not { } folder || !fileSystem.DirectoryExists(folder))
        {
            return [];
        }

        var index = Path.Combine(folder, IndexFileName);
        if (fileSystem.FileExists(index))
        {
            try
            {
                return JsonSerializer.Deserialize(fileSystem.ReadAllBytes(index), AgentJsonContext.Readable.ListChatSummary) ?? [];
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                LogReadFailed(logger, exception, index);
            }
        }

        return RebuildIndex(folder);
    }

    // Once per folder without an index (older history) or after corruption: O(total chat size).
    private List<ChatSummary> RebuildIndex(string folder)
    {
        List<ChatSummary> summaries =
        [
            .. fileSystem.EnumerateEntries(folder)
                .Where(static entry => !entry.IsDirectory && entry.Name.StartsWith(FilePrefix, StringComparison.Ordinal))
                .Select(entry => Read(entry.FullPath)?.ToSummary())
                .OfType<ChatSummary>(),
        ];

        try
        {
            WriteIndex(folder, summaries);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, folder);
        }

        return summaries;
    }

    private void WriteIndex(string folder, List<ChatSummary> summaries) =>
        fileSystem.WriteAllBytesAtomic(Path.Combine(folder, IndexFileName),
            JsonSerializer.SerializeToUtf8Bytes(summaries, AgentJsonContext.Readable.ListChatSummary));

    private ChatTranscript? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize(fileSystem.ReadAllBytes(path), AgentJsonContext.Readable.ChatTranscript);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogReadFailed(logger, exception, path);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save chat history to {Folder}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipped a corrupted history file {Path}")]
    private static partial void LogReadFailed(ILogger logger, Exception exception, string path);
}
