using CodeEditor.Core.Documents;
using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Search.Services;

/// <summary>Unsaved text of open documents for search, snapshotted on the UI thread.</summary>
public static class UnsavedTexts
{
    public static async Task<Dictionary<string, string>> SnapshotAsync(IDocumentService documents, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var unsaved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await dispatcher.InvokeAsync(() =>
        {
            foreach (var document in documents.Documents.Where(document => document.IsDirty))
            {
                unsaved[document.FilePath] = document.Buffer.GetText();
            }
        });

        return unsaved;
    }
}
