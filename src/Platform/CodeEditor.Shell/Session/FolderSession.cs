namespace CodeEditor.Shell.Session;

/// <summary>
/// The tabs of one folder, stored per folder: the folder reopens with them whichever window opens it, and windows of
/// different folders don't overwrite each other's tabs.
/// </summary>
public sealed record FolderSession
{
    public required string Folder { get; init; }

    public List<SessionTab> Tabs { get; init; } = [];

    public string? ActiveTab { get; init; }
}
