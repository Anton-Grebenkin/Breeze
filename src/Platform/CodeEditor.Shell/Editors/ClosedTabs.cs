namespace CodeEditor.Shell.Editors;

/// <summary>Recently closed files for "Reopen closed tab" (<c>Ctrl+Shift+T</c>), without duplicates.</summary>
public sealed class ClosedTabs
{
    public const int Capacity = 20;

    private readonly List<string> _paths = [];

    /// <summary>Paths in closing order; the last one is the most recent.</summary>
    public IReadOnlyList<string> Items => _paths;

    public void Remember(string path)
    {
        _paths.Remove(path);
        _paths.Add(path);
        if (_paths.Count > Capacity)
        {
            _paths.RemoveAt(0);
        }
    }

    /// <returns>The path of the last closed file, or <c>null</c> if the history is empty.</returns>
    public string? TakeLast()
    {
        if (_paths.Count == 0)
        {
            return null;
        }

        var path = _paths[^1];
        _paths.RemoveAt(_paths.Count - 1);
        return path;
    }
}
