namespace CodeEditor.Shell.Editors;

/// <summary>
/// Argument of the editor go-to command: 1-based line and column and selection length (0 means caret only).
/// </summary>
public sealed record EditorLocation(int Line, int Column = 1, int Length = 0)
{
    /// <summary>Keeps focus where it is: a single click on a search result leaves it in the list, as in VS Code.</summary>
    public bool PreserveFocus { get; init; }
}
