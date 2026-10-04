using CodeEditor.Core.Documents;
using CodeEditor.Core.Threading;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Services;

/// <summary>
/// Saves modified tabs before a build, test run or command, like Visual Studio's "save changes before build":
/// processes read files from disk while edits live in tabs. Files deleted or changed on disk (conflicts) are not
/// saved; the user decides.
/// </summary>
public sealed partial class SaveBeforeRun(IDocumentService documents, IUiDispatcher dispatcher, ILogger<SaveBeforeRun> logger)
{
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        IDocument[] dirty = [];
        await dispatcher.InvokeAsync(() => dirty = [.. documents.Documents.Where(IsSafeToSave)]);
        foreach (var document in dirty)
        {
            Task? saving = null;
            await dispatcher.InvokeAsync(() => saving = documents.SaveAsync(document, cancellationToken));
            try
            {
                await saving!;
            }
            catch (IOException exception)
            {
                // Run anyway: build errors will reveal the stale file on disk.
                LogSaveFailed(logger, document.FilePath, exception);
            }
        }
    }

    private static bool IsSafeToSave(IDocument document) => document is { IsDirty: true, IsDeletedOnDisk: false, HasExternalChanges: false };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save {Path} before running")]
    private static partial void LogSaveFailed(ILogger logger, string path, Exception exception);
}
