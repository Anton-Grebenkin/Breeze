using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Session;

/// <summary><c>state.json</c> in the user data folder; a corrupt file is treated as a first run.</summary>
public sealed partial class JsonSessionStore(IFileSystem fileSystem, UserDataPaths paths, ILogger<JsonSessionStore> logger) : ISessionStore
{
    public const string FileName = "state.json";

    private string FilePath => paths.File(FileName);

    public SessionState? Load()
    {
        try
        {
            return fileSystem.FileExists(FilePath)
                ? JsonSerializer.Deserialize(fileSystem.ReadAllBytes(FilePath), SessionJsonContext.Default.SessionState)
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogLoadFailed(logger, exception, FilePath);
            return null;
        }
    }

    public void Save(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (!fileSystem.DirectoryExists(paths.Root))
            {
                fileSystem.CreateDirectory(paths.Root);
            }

            fileSystem.WriteAllBytesAtomic(FilePath, JsonSerializer.SerializeToUtf8Bytes(state, SessionJsonContext.Default.SessionState));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, FilePath);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read state {Path}; session not restored")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save state {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string path);
}
