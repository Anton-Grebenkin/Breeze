using System.Globalization;
using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>Agent tools about the editor: open files and the active selection ("this spot" without copy-paste).</summary>
public sealed class EditorAgentTools(EditorAreaViewModel editors, IWorkspace workspace, IUiDispatcher dispatcher) : IAgentToolProvider
{
    public const string OpenDocumentsName = "get_open_documents";
    public const string SelectionName = "get_selection";

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(GetOpenDocumentsAsync, OpenDocumentsName, "Lists files open in editor tabs: relative path, '(unsaved)' for files with unsaved changes, '(active)' for the current tab.")),
        new ReadOnlyAIFunction(AIFunctionFactory.Create(GetSelectionAsync, SelectionName, "Returns the active editor file, caret position and the selected text (empty if nothing is selected).")),
    ];

    private async Task<string> GetOpenDocumentsAsync()
    {
        var lines = new List<string>();
        await dispatcher.InvokeAsync(() =>
        {
            foreach (var tab in editors.Tabs.Where(tab => tab.FilePath is not null))
            {
                var marks = (tab.IsDirty ? " (unsaved)" : string.Empty) + (ReferenceEquals(tab, editors.Active) ? " (active)" : string.Empty);
                lines.Add(workspace.RelativePath(tab.FilePath!) + marks);
            }
        });

        return lines.Count == 0 ? Strings.NoOpenFiles : string.Join('\n', lines);
    }

    private async Task<string> GetSelectionAsync()
    {
        string? result = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (editors.Active?.Editor is not TextEditorViewModel editor)
            {
                return;
            }

            var output = new StringBuilder();
            output.Append(CultureInfo.InvariantCulture, $"file: {workspace.RelativePath(editor.Document.FilePath)}\n");
            output.Append(CultureInfo.InvariantCulture, $"caret: line {editor.CaretLine}, column {editor.CaretColumn}\n");
            var selected = editor.SelectionLength > 0 ? editor.Document.Buffer.GetText(editor.SelectionStart, editor.SelectionLength) : string.Empty;
            output.Append(CultureInfo.InvariantCulture, $"selection ({selected.Length} chars):\n{selected}");
            result = output.ToString();
        });

        return result is null ? Strings.NoActiveEditor : ToolOutput.Limit(result);
    }
}
