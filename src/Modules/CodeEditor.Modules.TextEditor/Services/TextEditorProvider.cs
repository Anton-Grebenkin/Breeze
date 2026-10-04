using CodeEditor.Core.Documents;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// The text editor opens any text file; specialized editors take higher priority. Each editor has its own agent
/// changes (<see cref="AgentChangesViewModel"/>).
/// </summary>
/// <param name="reverter">Lazy: the reverter depends on the editor area, which itself collects editor providers.</param>
public sealed class TextEditorProvider(
    EditorSettings settings,
    EditorFocus focus,
    IAgentFileState fileState,
    IDocumentService documents,
    Lazy<IAgentChangeReverter> reverter,
    IUiDispatcher dispatcher,
    TimeProvider time) : IEditorProvider
{
    public int Priority => 0;

    public bool CanOpen(string filePath) => true;

    public object CreateEditor(IDocument document)
    {
        TextEditorViewModel? editor = null;
        var changes = new AgentChangesViewModel(document, fileState, documents, reverter.Value, dispatcher, time, line => editor?.GoToLine(line));
        editor = new TextEditorViewModel(document, settings, focus) { AgentChanges = changes };
        return editor;
    }
}
