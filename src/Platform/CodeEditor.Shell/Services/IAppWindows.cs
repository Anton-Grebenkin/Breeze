namespace CodeEditor.Shell.Services;

/// <summary>The other Breeze windows of this user: each window is its own process (ADR 0044).</summary>
public interface IAppWindows
{
    /// <summary>How many other windows are open.</summary>
    int CountOthers();

    /// <summary>Opens a new window: empty, or with a folder.</summary>
    void OpenNew(string? folder);

    /// <summary>Brings the window that has the folder open to the front; <c>false</c> if no window has it.</summary>
    Task<bool> TryActivateAsync(string folder);
}
