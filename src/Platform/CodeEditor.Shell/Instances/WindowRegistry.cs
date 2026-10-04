using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// The open windows of this user: one small file per process in <c>windows/</c> of the user data folder, written when
/// the window appears, changes folder or is activated, and deleted on exit. Entries of processes that have exited
/// (a crash) are deleted when read.
/// </summary>
public sealed partial class WindowRegistry(IFileSystem fileSystem, UserDataPaths paths, IProcessProbe processes, ILogger<WindowRegistry> logger)
{
    public const string FolderName = "windows";

    private const string Extension = ".json";

    private string Folder => paths.File(FolderName);

    public void Publish(WindowEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            if (!fileSystem.DirectoryExists(Folder))
            {
                fileSystem.CreateDirectory(Folder);
            }

            fileSystem.WriteAllBytesAtomic(FileOf(entry.ProcessId), JsonSerializer.SerializeToUtf8Bytes(entry, InstanceJsonContext.Default.WindowEntry));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogPublishFailed(logger, exception, entry.ProcessId);
        }
    }

    public void Remove(int processId) => TryDelete(FileOf(processId));

    /// <summary>The other running windows, the most recently active first.</summary>
    public IReadOnlyList<WindowEntry> Others(int currentProcessId)
    {
        if (!fileSystem.DirectoryExists(Folder))
        {
            return [];
        }

        var windows = new List<WindowEntry>();
        foreach (var file in fileSystem.EnumerateEntries(Folder).Where(entry => !entry.IsDirectory && entry.Name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)))
        {
            if (Read(file.FullPath) is not { } window)
            {
                continue;
            }

            if (processes.StartTimeOf(window.ProcessId) != window.StartTime)
            {
                TryDelete(file.FullPath);
            }
            else if (window.ProcessId != currentProcessId)
            {
                windows.Add(window);
            }
        }

        return [.. windows.OrderByDescending(window => window.LastActive)];
    }

    private string FileOf(int processId) => Path.Combine(Folder, processId.ToString(CultureInfo.InvariantCulture) + Extension);

    // A file being replaced by its window or corrupt is skipped this time; a corrupt one goes with its process.
    private WindowEntry? Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize(fileSystem.ReadAllBytes(path), InstanceJsonContext.Default.WindowEntry);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (fileSystem.FileExists(path))
            {
                fileSystem.DeleteFile(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogRemoveFailed(logger, exception, path);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not publish window {ProcessId}")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, int processId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not remove window entry {Path}")]
    private static partial void LogRemoveFailed(ILogger logger, Exception exception, string path);
}
