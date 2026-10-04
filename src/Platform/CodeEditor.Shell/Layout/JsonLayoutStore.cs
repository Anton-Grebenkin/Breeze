using System.Text.Json;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Layout;

/// <summary>
/// Layout in <c>layout.json</c> in the user data folder. A corrupt file doesn't block startup: a warning is logged
/// and the default layout is used. Writes are atomic via a temporary file.
/// </summary>
public sealed partial class JsonLayoutStore(UserDataPaths paths, ILogger<JsonLayoutStore> logger) : ILayoutStore
{
    private const string FileName = "layout.json";

    private string FilePath => paths.File(FileName);

    public LayoutState? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize(stream, LayoutJsonContext.Default.LayoutState);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogLoadFailed(logger, exception, FilePath);
            return null;
        }
    }

    public void Save(LayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        try
        {
            Directory.CreateDirectory(paths.Root);
            var temporary = FilePath + ".tmp";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(state, LayoutJsonContext.Default.LayoutState));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, FilePath);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read layout {Path}; using the default layout")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save layout {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string path);
}
