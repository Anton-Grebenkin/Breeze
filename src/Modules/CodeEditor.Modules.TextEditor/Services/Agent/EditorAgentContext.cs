using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Editor context for the agent: the open file, caret line and selected lines, without text (as in Copilot).
/// The model understands "this method" right away and reads contents with tools when needed.
/// </summary>
public sealed class EditorAgentContext(EditorAreaViewModel editors, IWorkspace workspace, IUiDispatcher dispatcher) : IAgentContextProvider
{
    public async ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IncludeActiveEditor)
        {
            return [];
        }

        string? line = null;
        await dispatcher.InvokeAsync(() => line = Describe());
        return line is null ? [] : [line];
    }

    private string? Describe()
    {
        if (editors.Active is not { FilePath: { } file } tab)
        {
            return null;
        }

        var path = workspace.RelativePath(file);
        var unsaved = tab.IsDirty ? Strings.ContextUnsaved : string.Empty;
        if (tab.Editor is not TextEditorViewModel editor)
        {
            return Format(Strings.ContextOpenFile, path, unsaved);
        }

        var selection = editor.SelectionLines is var (first, last)
            ? first == last
                ? Format(Strings.ContextSelectionInLine, first)
                : Format(Strings.ContextSelectedLines, first, last)
            : string.Empty;
        return Format(Strings.ContextOpenFileAtCaret, path, unsaved, editor.CaretLine, selection);
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
