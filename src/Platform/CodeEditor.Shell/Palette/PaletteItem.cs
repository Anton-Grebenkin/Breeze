namespace CodeEditor.Shell.Palette;

/// <summary>
/// A quick input row: title with highlighted matches, detail (the file's folder) and keybinding.
/// </summary>
/// <param name="Id">What to run: a command id, file path or line number, as the provider decides.</param>
/// <param name="Highlights">Indices of <paramref name="Title"/> characters that matched the query.</param>
public sealed record PaletteItem(
    string Id,
    string Title,
    string? Shortcut,
    IReadOnlyList<int> Highlights,
    bool IsRecent)
{
    /// <summary>Secondary text right of the title; the folder for a file.</summary>
    public string? Detail { get; init; }
}
