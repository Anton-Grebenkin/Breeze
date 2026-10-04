using System.Text.Json;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Workspace;

/// <summary>
/// Recently opened folders, newest first, stored in <c>recent-folders.json</c> in user data. Paths compare
/// case-insensitively, as on Windows.
/// </summary>
public sealed partial class RecentFolders
{
    public const int Capacity = 10;

    private const string FileName = "recent-folders.json";

    private readonly UserDataPaths _paths;
    private readonly ILogger<RecentFolders> _logger;
    private readonly List<string> _folders;

    public RecentFolders(UserDataPaths paths, ILogger<RecentFolders> logger)
    {
        _paths = paths;
        _logger = logger;
        _folders = Load();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<string> Items => _folders;

    public void Add(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        _folders.RemoveAll(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase));
        _folders.Insert(0, folder);
        if (_folders.Count > Capacity)
        {
            _folders.RemoveRange(Capacity, _folders.Count - Capacity);
        }

        Save();
    }

    public void Remove(string folder)
    {
        if (_folders.RemoveAll(existing => string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            Save();
        }
    }

    public void Clear()
    {
        _folders.Clear();
        Save();
    }

    private List<string> Load()
    {
        var path = _paths.File(FileName);
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, ShellJsonContext.Default.ListString) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
            return [];
        }
    }

    private void Save()
    {
        var path = _paths.File(FileName);
        try
        {
            Directory.CreateDirectory(_paths.Root);
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(_folders, ShellJsonContext.Default.ListString));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStorageFailed(_logger, exception, path);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read or save the recent folders list {Path}")]
    private static partial void LogStorageFailed(ILogger logger, Exception exception, string path);
}
