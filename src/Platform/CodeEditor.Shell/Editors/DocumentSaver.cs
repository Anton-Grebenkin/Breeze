using System.Globalization;
using CodeEditor.Core.Documents;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Editors;

/// <summary>
/// Saving with checks: overwriting a file changed on disk needs confirmation, closing asks once for all dirty files,
/// write errors go to the status bar.
/// </summary>
public sealed class DocumentSaver(IDocumentService documents, IDialogService dialogs, StatusBarViewModel statusBar)
{
    /// <param name="ask">
    /// Ask before overwriting a file changed on disk. Without asking (auto save) such a file is left alone: the tab
    /// shows the conflict, and the user's own save asks.
    /// </param>
    /// <returns><c>false</c> if the user declined to overwrite or the write failed.</returns>
    public async Task<bool> SaveAsync(IDocument document, bool ask = true)
    {
        if (document.HasExternalChanges && !(ask && dialogs.Confirm(
                string.Format(CultureInfo.CurrentCulture, Strings.FileChangedOnDisk, document.Name), Strings.OverwriteQuestion, Strings.Overwrite)))
        {
            return false;
        }

        try
        {
            await documents.SaveAsync(document);
            return true;
        }
        catch (DocumentChangedOnDiskException)
        {
            // Found only at the write; the document is flagged now, so the second pass asks.
            return ask && await SaveAsync(document, ask);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = string.Format(CultureInfo.CurrentCulture, Strings.SaveFailed, document.Name, exception.Message);
            return false;
        }
    }

    /// <summary>Saves dirty tab documents one by one; stops at the first refusal or error.</summary>
    public async Task SaveAllAsync(IEnumerable<EditorTab> tabs)
    {
        foreach (var document in DirtyDocuments(tabs))
        {
            if (!await SaveAsync(document))
            {
                return;
            }
        }
    }

    /// <summary>Asks about unsaved tab documents. Returns <c>false</c> if the action was cancelled.</summary>
    public Task<bool> ResolveUnsavedAsync(IEnumerable<EditorTab> tabs) => ResolveUnsavedAsync(DirtyDocuments(tabs));

    /// <summary>Asks about unsaved files and saves them if told to. Returns <c>false</c> if cancelled.</summary>
    public async Task<bool> ResolveUnsavedAsync(IReadOnlyList<IDocument> dirty)
    {
        if (dirty.Count == 0)
        {
            return true;
        }

        switch (dialogs.AskToSave([.. dirty.Select(document => document.Name)]))
        {
            case SaveChoice.Save:
                foreach (var document in dirty)
                {
                    if (!await SaveAsync(document))
                    {
                        return false;
                    }
                }

                return true;
            case SaveChoice.DontSave:
                return true;
            default:
                return false;
        }
    }

    private static List<IDocument> DirtyDocuments(IEnumerable<EditorTab> tabs) =>
        [.. tabs.OfType<EditorTabViewModel>().Where(tab => tab.IsDirty).Select(tab => tab.Document)];
}
