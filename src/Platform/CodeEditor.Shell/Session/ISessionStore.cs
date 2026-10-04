namespace CodeEditor.Shell.Session;

/// <summary>Session state storage; read and write errors don't interrupt work.</summary>
public interface ISessionStore
{
    SessionState? Load();

    void Save(SessionState state);
}
