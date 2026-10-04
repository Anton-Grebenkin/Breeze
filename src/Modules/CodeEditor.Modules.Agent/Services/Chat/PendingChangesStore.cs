using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Keeps agent edits awaiting review (<see cref="IAgentFileState.Changes"/>) in <c>.breeze/agent/pending-changes.json</c>,
/// so after a restart or reopening the folder the files are still highlighted where the review stopped (ADR 0040).
/// Saved on every change of the list; loaded when a folder opens. Entries whose file is back to the original are dropped.
/// </summary>
public sealed partial class PendingChangesStore : IDisposable
{
    public const string FileName = "pending-changes.json";

    private const string FilesProperty = "files";
    private const string PathProperty = "path";
    private const string OriginalProperty = "original";

    private readonly AgentFileState _fileState;
    private readonly IWorkspace _workspace;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PendingChangesStore> _logger;
    private readonly Lock _lock = new();
    private bool _loading;

    public PendingChangesStore(AgentFileState fileState, IWorkspace workspace, IFileSystem fileSystem, ILogger<PendingChangesStore> logger)
    {
        _fileState = fileState;
        _workspace = workspace;
        _fileSystem = fileSystem;
        _logger = logger;
        _workspace.Changed += OnWorkspaceChanged;
        _fileState.ChangesChanged += OnChangesChanged;
        _fileState.Written += OnWritten;
        Load();
    }

    public void Dispose()
    {
        _workspace.Changed -= OnWorkspaceChanged;
        _fileState.ChangesChanged -= OnChangesChanged;
        _fileState.Written -= OnWritten;
    }

    /// <summary>Reads the list of the open folder into the file state; without a folder the list is empty.</summary>
    public void Load()
    {
        lock (_lock)
        {
            _loading = true;
            try
            {
                _fileState.ReplaceChanges(Read());
            }
            finally
            {
                _loading = false;
            }
        }
    }

    private Dictionary<string, string?> Read()
    {
        var changes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (FilePath() is not { } file || !_fileSystem.FileExists(file))
        {
            return changes;
        }

        try
        {
            using var json = JsonDocument.Parse(_fileSystem.ReadAllBytes(file));
            foreach (var entry in json.RootElement.GetProperty(FilesProperty).EnumerateArray())
            {
                var path = Path.GetFullPath(Path.Combine(_workspace.Root!, entry.GetProperty(PathProperty).GetString()!));
                var original = entry.GetProperty(OriginalProperty).GetString();
                if (IsPending(path, original))
                {
                    changes[path] = original;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            LogNotRead(_logger, exception.Message);
        }

        return changes;
    }

    // Still awaiting review: the file differs from its original (a created file still exists).
    private bool IsPending(string path, string? original)
    {
        if (!path.StartsWith(_workspace.Root!, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return _fileSystem.FileExists(path) ? original is null || _fileSystem.ReadAllText(path) != original : original is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            if (_loading || FilePath() is not { } file)
            {
                return;
            }

            try
            {
                var changes = _fileState.Changes.ToArray();
                if (changes.Length == 0)
                {
                    if (_fileSystem.FileExists(file))
                    {
                        _fileSystem.DeleteFile(file);
                    }

                    return;
                }

                AgentDataFolder.Ensure(_workspace, _fileSystem, Path.GetDirectoryName(file)!);
                _fileSystem.WriteAllBytesAtomic(file, Serialize(changes));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LogNotSaved(_logger, exception.Message);
            }
        }
    }

    private byte[] Serialize(KeyValuePair<string, string?>[] changes)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray(FilesProperty);
            foreach (var (path, original) in changes.OrderBy(static change => change.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteStartObject();
                writer.WriteString(PathProperty, _workspace.RelativePath(path));
                writer.WriteString(OriginalProperty, original);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private string? FilePath() => AgentDataFolder.PathOf(_workspace, FileName);

    private void OnWorkspaceChanged(object? sender, EventArgs e) => Load();

    private void OnChangesChanged(object? sender, string? path) => Save();

    private void OnWritten(object? sender, string path) => Save();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pending agent changes were not read: {Error}")]
    private static partial void LogNotRead(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pending agent changes were not saved: {Error}")]
    private static partial void LogNotSaved(ILogger logger, string error);
}
