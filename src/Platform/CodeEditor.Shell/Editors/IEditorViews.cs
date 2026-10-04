namespace CodeEditor.Shell.Editors;

/// <summary>
/// Editor tabs without a file (ADR 0031): git diffs, container logs, diagram previews. A module opens a view by id;
/// opening the same id again activates the existing tab. The view is resolved by ViewModel type through the view
/// registry, as for tool windows. Call on the UI thread.
/// </summary>
public interface IEditorViews
{
    /// <returns>The view tab; closing it disposes the ViewModel if it is <see cref="IDisposable"/>.</returns>
    EditorTab Open(EditorViewRequest request);

    /// <summary>Closes the view tab if it is open.</summary>
    void Close(string id);
}
