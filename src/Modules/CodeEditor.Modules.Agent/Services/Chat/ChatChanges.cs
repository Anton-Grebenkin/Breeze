using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Text;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Agent edits in the current chat: the original text of each changed file (<see cref="IAgentFileState.Changes"/>) and
/// the current one, from the open tab (with unsaved changes) or else from disk. Backs <c>get_changes</c>, the critic and
/// the summary. Files the user reverted to the original are left out.
/// </summary>
public sealed class ChatChanges(IAgentFileState fileState, IDocumentService documents, IFileSystem fileSystem, IWorkspace workspace, IUiDispatcher dispatcher)
{
    public async Task<IReadOnlyList<FileChange>> CollectAsync()
    {
        var changes = new List<FileChange>();
        foreach (var (path, original) in fileState.Changes.OrderBy(change => change.Key, StringComparer.OrdinalIgnoreCase))
        {
            var current = await CurrentTextAsync(path);
            if (!string.Equals(original, current, StringComparison.Ordinal))
            {
                changes.Add(new FileChange(path, workspace.RelativePath(path), original, current));
            }
        }

        return changes;
    }

    public static string Diff(IEnumerable<FileChange> changes) =>
        string.Join('\n', changes.Select(change => UnifiedDiff.Format(change.RelativePath, change.Original, change.Current)).Where(diff => diff.Length > 0));

    /// <summary>Current file text: from the open tab (with unsaved changes), else from disk; <c>null</c> if there is no file.</summary>
    public async Task<string?> CurrentTextAsync(string path)
    {
        string? open = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (documents.TryGet(path, out var document))
            {
                open = document.Buffer.GetText();
            }
        });

        if (open is not null)
        {
            return open;
        }

        return fileSystem.FileExists(path) ? TextFileCodec.Decode(fileSystem.ReadAllBytes(path))?.Text : null;
    }
}
