namespace CodeEditor.Shell.Services;

/// <summary>
/// System folder and file pickers. Implemented in Shell.Wpf, faked in tests.
/// </summary>
public interface IFileDialogs
{
    /// <summary>The chosen folder, or <c>null</c> if the user cancelled.</summary>
    string? PickFolder(string title);

    /// <summary>The chosen file, or <c>null</c> if the user cancelled.</summary>
    string? PickFile(string title);
}
