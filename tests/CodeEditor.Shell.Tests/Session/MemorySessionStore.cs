using CodeEditor.Shell.Session;

namespace CodeEditor.Shell.Tests.Session;

/// <summary>Session state in memory: the global state and the tabs of each folder.</summary>
internal sealed class MemorySessionStore : ISessionStore
{
    public SessionState? State { get; set; }

    public Dictionary<string, FolderSession> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SessionState? Load() => State;

    public void Save(SessionState state) => State = state;

    public FolderSession? LoadFolder(string folder) => Folders.GetValueOrDefault(folder);

    public void SaveFolder(FolderSession session) => Folders[session.Folder] = session;
}
