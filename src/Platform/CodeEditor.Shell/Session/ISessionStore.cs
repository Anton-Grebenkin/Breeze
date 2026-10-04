namespace CodeEditor.Shell.Session;

/// <summary>Session state storage; read and write errors don't interrupt work.</summary>
public interface ISessionStore
{
    SessionState? Load();

    void Save(SessionState state);

    /// <summary>The tabs saved for a folder; <c>null</c> if none were saved.</summary>
    FolderSession? LoadFolder(string folder);

    void SaveFolder(FolderSession session);
}
