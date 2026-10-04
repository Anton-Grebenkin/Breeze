namespace CodeEditor.Shell.Services;

/// <summary>
/// Can delay or cancel closing the window, e.g. to ask about unsaved files. Modules register implementations in DI;
/// the window asks all of them before closing.
/// </summary>
public interface IShutdownGuard
{
    /// <summary>Returns <c>false</c> if the user cancelled closing.</summary>
    Task<bool> CanShutdownAsync();
}
