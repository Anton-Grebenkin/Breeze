using System.Globalization;
using System.Text;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Resources;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Reverts the agent's changes to a file (<see cref="IAgentChangeReverter"/>): the original text goes into the tab as
/// one undo step (so the revert itself can be undone with <c>Ctrl+Z</c>) and is saved, since agent edits are already on
/// disk (ADR 0040); unsaved typing of the user stays unsaved. A file the agent created has its tab closed and goes to
/// the recycle bin; a deleted file is written back and opened.
/// </summary>
public sealed class AgentChangeReverter(EditorAreaViewModel editors, IFileSystem fileSystem, IUiDispatcher dispatcher, IDocumentService documents)
    : IAgentChangeReverter
{
    public async Task RevertAsync(string path, string? originalText)
    {
        if (originalText is null)
        {
            await RemoveCreatedAsync(path);
            return;
        }

        if (!fileSystem.FileExists(path))
        {
            fileSystem.WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(originalText));
            await OpenAsync(path);
            return;
        }

        var tab = await OpenAsync(path);
        Task? saving = null;
        await dispatcher.InvokeAsync(() =>
        {
            var document = tab.Document;
            var wasSaved = !document.IsDirty;
            document.Buffer.ReplaceAll([new TextReplacement(0, document.Buffer.Length, originalText)]);
            if (wasSaved && !document.IsDeletedOnDisk)
            {
                saving = documents.SaveAsync(document);
            }
        });

        if (saving is not null)
        {
            await saving;
        }
    }

    private async Task RemoveCreatedAsync(string path)
    {
        EditorTabViewModel? tab = null;
        await dispatcher.InvokeAsync(() => tab = editors.Tabs.OfType<EditorTabViewModel>().FirstOrDefault(candidate => string.Equals(candidate.Document.FilePath, path, StringComparison.OrdinalIgnoreCase)));
        if (tab is not null)
        {
            // The whole agent-created file goes away: drop unsaved edits so no "Save?" prompt appears.
            await dispatcher.InvokeAsync(() => tab.Document.Buffer.MarkSaved());
            Task<bool>? closing = null;
            await dispatcher.InvokeAsync(() => closing = editors.CloseAsync(tab));
            await closing!;
        }

        if (fileSystem.FileExists(path))
        {
            fileSystem.DeleteToRecycleBin(path);
        }
    }

    private async Task<EditorTabViewModel> OpenAsync(string path)
    {
        Task<EditorTabViewModel?>? opening = null;
        await dispatcher.InvokeAsync(() => opening = editors.OpenTextAsync(new OpenFileRequest(path)));
        return await opening! ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.CannotOpenFile, path));
    }
}
