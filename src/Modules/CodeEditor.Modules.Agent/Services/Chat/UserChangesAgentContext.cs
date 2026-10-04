using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>
/// Reports in the <c>&lt;context&gt;</c> block what the user did to agent files between messages (as Copilot's
/// <c>EditedFileEvents</c>): changed or deleted a file after the agent saw it, or rejected agent edits. Otherwise the
/// model learned it only from an error on the next edit (ADR 0014). Each file is named once: the agent forgets it and
/// re-reads it before editing. Text is compared by version hash, no copies are kept; O(files known to the agent).
/// </summary>
public sealed class UserChangesAgentContext(AgentFileState fileState, ChatChanges changes, IWorkspace workspace) : IAgentContextProvider
{
    public async ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        // Path → note: rejected edits or deleted; null means changed.
        var changed = new SortedDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in fileState.TakeRejected())
        {
            changed[path] = Strings.UserRejectedChanges;
        }

        foreach (var path in fileState.KnownFiles.Where(path => !changed.ContainsKey(path)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = await changes.CurrentTextAsync(path);
            if (text is null)
            {
                changed[path] = Strings.UserDeletedFile;
            }
            else if (!fileState.IsKnownVersion(path, text))
            {
                changed[path] = null;
            }
        }

        foreach (var path in changed.Keys)
        {
            fileState.Forget(path);
        }

        if (changed.Count == 0)
        {
            return [];
        }

        var files = changed.Select(file => workspace.RelativePath(file.Key) + (file.Value is null ? string.Empty : $" ({file.Value})"));
        return [string.Format(CultureInfo.CurrentCulture, Strings.UserChangedFiles, string.Join(", ", files))];
    }
}
