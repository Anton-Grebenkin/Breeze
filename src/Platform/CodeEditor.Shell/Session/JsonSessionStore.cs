using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Session;

/// <summary>
/// <c>state.json</c> in the user data folder, plus <c>sessions/&lt;key&gt;.json</c> with the tabs of each folder. A
/// corrupt file is treated as a first run.
/// </summary>
public sealed partial class JsonSessionStore(IFileSystem fileSystem, UserDataPaths paths, ILogger<JsonSessionStore> logger) : ISessionStore
{
    public const string FileName = "state.json";
    public const string FoldersDirectory = "sessions";

    private const int FolderKeyLength = 16;

    public SessionState? Load() => Read(paths.File(FileName), SessionJsonContext.Default.SessionState);

    public void Save(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Write(paths.File(FileName), state, SessionJsonContext.Default.SessionState);
    }

    public FolderSession? LoadFolder(string folder) => Read(FolderFile(folder), SessionJsonContext.Default.FolderSession);

    public void SaveFolder(FolderSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Write(FolderFile(session.Folder), session, SessionJsonContext.Default.FolderSession);
    }

    /// <summary>The file name of a folder's session: a hash of the path, which compares case-insensitively.</summary>
    public static string FolderKey(string folder)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)).ToUpperInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..FolderKeyLength];
    }

    private string FolderFile(string folder) => Path.Combine(paths.File(FoldersDirectory), FolderKey(folder) + ".json");

    private T? Read<T>(string path, JsonTypeInfo<T> type)
        where T : class
    {
        try
        {
            return fileSystem.FileExists(path) ? JsonSerializer.Deserialize(fileSystem.ReadAllBytes(path), type) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogLoadFailed(logger, exception, path);
            return null;
        }
    }

    private void Write<T>(string path, T value, JsonTypeInfo<T> type)
    {
        try
        {
            var folder = Path.GetDirectoryName(path)!;
            if (!fileSystem.DirectoryExists(folder))
            {
                fileSystem.CreateDirectory(folder);
            }

            fileSystem.WriteAllBytesAtomic(path, JsonSerializer.SerializeToUtf8Bytes(value, type));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read state {Path}; session not restored")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save state {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string path);
}
