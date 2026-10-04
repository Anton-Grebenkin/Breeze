namespace CodeEditor.Shell.ToolWindows;

/// <summary>
/// A tool window declared by a module. Its content (ViewModel) is created on first show; the view is resolved by
/// ViewModel type (views are registered in the module's *.Wpf assembly).
/// </summary>
/// <param name="Icon">Codicon name (<see cref="IconNames"/>) for the activity bar.</param>
public sealed record ToolWindowDefinition(
    string Id,
    string Title,
    string Icon,
    ToolWindowLocation Location,
    Func<object> CreateContent)
{
    /// <summary>Order among tool windows of the same area.</summary>
    public int Order { get; init; }

    /// <summary>Keybinding of the "show tool window" command, e.g. <c>Ctrl+Shift+U</c>.</summary>
    public string? Keybinding { get; init; }
}
