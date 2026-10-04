namespace CodeEditor.Shell.Editors;

/// <summary>
/// Recently active files, newest first, including closed ones: shown for an empty <c>Ctrl+P</c> query and ranked
/// higher in results. Persisted in the session state.
/// </summary>
public sealed class RecentFiles : IDisposable
{
    public const int Capacity = 50;

    private readonly EditorAreaViewModel _editors;
    private readonly List<string> _paths = [];

    public RecentFiles(EditorAreaViewModel editors)
    {
        _editors = editors;
        _editors.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    public IReadOnlyList<string> Items => _paths;

    /// <summary>Restores the list from the previous session, newest first.</summary>
    public void Restore(IEnumerable<string> paths)
    {
        _paths.Clear();
        _paths.AddRange(paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).Take(Capacity));
    }

    public void Dispose() => _editors.ActiveDocumentChanged -= OnActiveDocumentChanged;

    private void OnActiveDocumentChanged(object? sender, EventArgs e)
    {
        if (_editors.Active?.FilePath is not { } path)
        {
            return;
        }

        _paths.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        _paths.Insert(0, path);
        if (_paths.Count > Capacity)
        {
            _paths.RemoveAt(_paths.Count - 1);
        }
    }
}
