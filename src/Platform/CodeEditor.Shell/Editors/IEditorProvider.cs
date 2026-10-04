using CodeEditor.Core.Documents;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Document editor provider, registered by a module in DI; the highest-priority provider wins.
/// </summary>
public interface IEditorProvider
{
    int Priority { get; }

    bool CanOpen(string filePath);

    /// <summary>Creates the editor ViewModel; its view is resolved by type through the view registry.</summary>
    object CreateEditor(IDocument document);
}
