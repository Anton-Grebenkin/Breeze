using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Explorer.Services;

/// <summary>
/// Files with agent edits awaiting review and the folders that contain them, for highlighting in the tree (ADR 0040).
/// A snapshot of path sets is rebuilt on every change of the list, so lookups from tree nodes are O(1).
/// </summary>
public sealed class AgentChangeMarks : IDisposable
{
    private readonly IAgentFileState _fileState;
    private Snapshot _snapshot = Snapshot.Empty;

    public AgentChangeMarks(IAgentFileState fileState)
    {
        _fileState = fileState;
        _fileState.ChangesChanged += OnChangesChanged;
        _fileState.Written += OnWritten;
        Rebuild();
    }

    /// <summary>The marks changed; may come from a background thread.</summary>
    public event EventHandler? Changed;

    public bool IsChanged(string path) => _snapshot.Files.Contains(path);

    public bool Contains(string folder) => _snapshot.Folders.Contains(folder);

    public void Dispose()
    {
        _fileState.ChangesChanged -= OnChangesChanged;
        _fileState.Written -= OnWritten;
    }

    private void OnChangesChanged(object? sender, string? path) => Rebuild();

    private void OnWritten(object? sender, string path) => Rebuild();

    // O(files × depth).
    private void Rebuild()
    {
        var files = new HashSet<string>(_fileState.Changes.Keys, StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            // A folder already in the set has all its ancestors there too.
            var folder = Path.GetDirectoryName(file);
            while (folder is not null && folders.Add(folder))
            {
                folder = Path.GetDirectoryName(folder);
            }
        }

        _snapshot = new Snapshot(files, folders);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record Snapshot(HashSet<string> Files, HashSet<string> Folders)
    {
        public static Snapshot Empty { get; } = new([], []);
    }
}
