namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// Tool window content that takes focus when a command shows it (e.g. <c>Ctrl+Shift+F</c> focuses the search box, as
/// in VS Code). The view sets focus on the event.
/// </summary>
public interface IFocusableContent
{
    event EventHandler? FocusRequested;

    void RequestFocus();
}
