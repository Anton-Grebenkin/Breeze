namespace CodeEditor.Shell.Session;

/// <summary>State kept between runs in <c>state.json</c>: folder, tabs, recent commands and files.</summary>
public sealed record SessionState
{
    public string? Folder { get; init; }

    public List<SessionTab> Tabs { get; init; } = [];

    public string? ActiveTab { get; init; }

    public List<string> RecentCommands { get; init; } = [];

    public List<string> RecentFiles { get; init; } = [];
}
