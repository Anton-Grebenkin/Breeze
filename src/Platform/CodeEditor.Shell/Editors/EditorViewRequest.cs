namespace CodeEditor.Shell.Editors;

/// <summary>A view for an editor tab (<see cref="IEditorViews.Open"/>).</summary>
/// <param name="Id">Tab key, e.g. "git.diff:src/A.cs", "docker.logs:api".</param>
/// <param name="CreateContent">Creates the tab ViewModel; called only if the tab isn't open yet.</param>
public sealed record EditorViewRequest(string Id, string Title, Func<object> CreateContent)
{
    public string? ToolTip { get; init; }

    /// <summary>Opens in the group to the side of the current file.</summary>
    public bool ToTheSide { get; init; }

    /// <summary>Preview tab: the next preview replaces it until it is pinned with a double click.</summary>
    public bool Preview { get; init; }
}
