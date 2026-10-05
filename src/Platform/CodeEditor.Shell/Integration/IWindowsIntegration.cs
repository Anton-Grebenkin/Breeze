namespace CodeEditor.Shell.Integration;

/// <summary>
/// Whether this build registers itself in Windows Explorer. Only an installed build does: a portable or development
/// one has no permanent path to register.
/// </summary>
public interface IWindowsIntegration
{
    bool IsAvailable { get; }
}
