namespace CodeEditor.Shell.Session;

/// <summary>
/// State kept between runs in <c>state.json</c>: the last folder, recent commands and files. Tabs live per folder
/// (<see cref="FolderSession"/>); <see cref="Tabs"/> and <see cref="ActiveTab"/> are read once from older files.
/// </summary>
public sealed record SessionState
{
    public string? Folder { get; init; }

    public List<SessionTab> Tabs { get; init; } = [];

    public string? ActiveTab { get; init; }

    public List<string> RecentCommands { get; init; } = [];

    public List<string> RecentFiles { get; init; } = [];
}
