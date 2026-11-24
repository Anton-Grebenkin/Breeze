using System.Globalization;
using System.Text;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Plans agent edits against the current text (including unsaved changes). Guards first: the file is not a secret or
/// in a service folder, the agent has read it and it hasn't changed since (after a context compaction, fragments found
/// exactly once are enough), and the size is reasonable. Fragments are located by <see cref="FragmentLocator"/>, a line
/// hint picks among repeated ones; line endings follow the file (models write <c>\n</c>, files may be CRLF). Both the
/// preview and the edit itself are built from the plan.
/// </summary>
public sealed class EditPlanner(IWorkspace workspace, IFileSystem fileSystem, IDocumentService documents, IUiDispatcher dispatcher, IAgentFileState fileState)
{
    /// <summary>Beyond this the edit is hardly targeted: the model should split it or recreate the file.</summary>
    public const int MaxEditCharacters = 100_000;

    public async Task<IReadOnlyList<PlannedFileEdit>> PlanAsync(IReadOnlyList<FileEdit> edits)
    {
        if (edits.Count == 0)
        {
            throw new AgentToolException(Strings.NoEdits);
        }

        if (edits.Sum(edit => (long)(edit.NewText?.Length ?? 0) + (edit.OldText?.Length ?? 0)) > MaxEditCharacters)
        {
            throw Error(Strings.EditTooLarge, MaxEditCharacters);
        }

        // Without a path the edit would resolve to the workspace root and fail with a confusing "file '.' not found".
        if (edits.Any(edit => string.IsNullOrWhiteSpace(edit.Path)))
        {
            throw new AgentToolException(Strings.EditWithoutPath);
        }

        var plans = new List<PlannedFileEdit>();
        foreach (var group in edits.GroupBy(edit => WorkspacePaths.Resolve(workspace, edit.Path), StringComparer.OrdinalIgnoreCase))
        {
            plans.Add(await PlanFileAsync(group.Key, [.. group]));
        }

        return plans;
    }

    private async Task<PlannedFileEdit> PlanFileAsync(string path, IReadOnlyList<FileEdit> edits)
    {
        var relative = workspace.RelativePath(path);
        SensitivePaths.EnsureWritable(relative);
        var text = await ReadTextAsync(path, relative);
        var crlf = text.Contains("\r\n", StringComparison.Ordinal);
        if (fileState.CheckEditable(path, relative, text) is { } problem && !(fileState.WasForgotten(path) && AllFoundOnce(text, edits, crlf)))
        {
            throw new AgentToolException(problem);
        }

        var located = Locate(text, edits, relative, crlf).OrderBy(fragment => fragment.Offset).ToList();
        var replacements = located.Select(fragment => new TextReplacement(fragment.Offset, fragment.Length, fragment.NewText)).ToList();
        EnsureNoOverlap(replacements, relative);
        return new PlannedFileEdit(path, relative, text, Apply(text, replacements), replacements)
        {
            Warnings = [.. located.Select(fragment => fragment.Warning).OfType<string>()],
        };
    }

    // apply_edits fragments are independent; apply_patch hunks depend on their order (HunkLocator).
    private static List<LocatedFragment> Locate(string text, IReadOnlyList<FileEdit> edits, string relative, bool crlf)
    {
        if (edits.Any(edit => string.IsNullOrEmpty(edit.OldText)))
        {
            throw Error(Strings.EmptyOldText, relative);
        }

        var hunks = new HunkLocator(text, relative);
        var hunksLeft = edits.Where(edit => edit.IsHunk).CountBy(edit => edit.OldText, StringComparer.Ordinal).ToDictionary(StringComparer.Ordinal);
        var located = new List<LocatedFragment>(edits.Count);
        foreach (var edit in edits)
        {
            var (oldText, newText) = (Normalize(edit.OldText, crlf), Normalize(edit.NewText ?? string.Empty, crlf));
            located.Add(edit.IsHunk
                ? hunks.Locate(oldText, newText, edit.Anchors, repeatsLater: --hunksLeft[edit.OldText] > 0)
                : FragmentLocator.Locate(text, oldText, newText, relative, edit.Line));
        }

        return located;
    }

    // Compaction took the file text out of the conversation, but fragments found exactly once show the model still
    // knows it: reading the file again would only cost a step.
    private static bool AllFoundOnce(string text, IReadOnlyList<FileEdit> edits, bool crlf) =>
        edits.All(edit => edit.Anchors.Count == 0 && !string.IsNullOrEmpty(edit.OldText) && Normalize(edit.OldText, crlf) is var old
            && text.IndexOf(old, StringComparison.Ordinal) is var first and >= 0 && text.IndexOf(old, first + 1, StringComparison.Ordinal) < 0);

    private static void EnsureNoOverlap(List<TextReplacement> replacements, string relative)
    {
        for (var i = 1; i < replacements.Count; i++)
        {
            if (replacements[i].Offset < replacements[i - 1].Offset + replacements[i - 1].Length)
            {
                throw Error(Strings.EditsOverlap, relative);
            }
        }
    }

    // Replacements are sorted and non-overlapping, so one forward pass builds the result in O(n).
    private static string Apply(string text, List<TextReplacement> replacements)
    {
        var result = new StringBuilder(text.Length);
        var position = 0;
        foreach (var replacement in replacements)
        {
            result.Append(text, position, replacement.Offset - position).Append(replacement.Text);
            position = replacement.Offset + replacement.Length;
        }

        return result.Append(text, position, text.Length - position).ToString();
    }

    private static AgentToolException Error(string format, params object[] arguments) =>
        new(string.Format(CultureInfo.CurrentCulture, format, arguments));

    private static string Normalize(string text, bool crlf)
    {
        var lf = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        return crlf ? lf.Replace("\n", "\r\n", StringComparison.Ordinal) : lf;
    }

    private async Task<string> ReadTextAsync(string file, string relative)
    {
        string? open = null;
        await dispatcher.InvokeAsync(() =>
        {
            if (documents.TryGet(file, out var document))
            {
                open = document.Buffer.GetText();
            }
        });

        if (open is not null)
        {
            return open;
        }

        if (!fileSystem.FileExists(file))
        {
            throw Error(Strings.FileNotFound, relative);
        }

        return TextFileCodec.Decode(fileSystem.ReadAllBytes(file))?.Text
            ?? throw Error(Strings.BinaryFile, relative);
    }
}
