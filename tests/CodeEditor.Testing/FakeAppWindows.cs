using CodeEditor.Shell.Services;

namespace CodeEditor.Testing;

/// <summary>Other windows without processes: preset folders are "open" elsewhere; new windows are recorded.</summary>
public sealed class FakeAppWindows : IAppWindows
{
    /// <summary>Folders open in other windows.</summary>
    public HashSet<string> OtherFolders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Other windows besides those with <see cref="OtherFolders"/>: empty ones.</summary>
    public int EmptyWindows { get; set; }

    /// <summary>New windows asked for: the folder or <c>null</c> for an empty one.</summary>
    public List<string?> Opened { get; } = [];

    public List<string> Activated { get; } = [];

    public int CountOthers() => OtherFolders.Count + EmptyWindows;

    public void OpenNew(string? folder) => Opened.Add(folder);

    public Task<bool> TryActivateAsync(string folder)
    {
        if (!OtherFolders.Contains(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder))))
        {
            return Task.FromResult(false);
        }

        Activated.Add(folder);
        return Task.FromResult(true);
    }
}
