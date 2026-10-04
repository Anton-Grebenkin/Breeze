using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Word;
using CodeEditor.Modules.Documents.Resources;
using static CodeEditor.Modules.Documents.Services.Changes.ChangePreviews;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// Word edits for <c>document_change</c>: creation from Markdown, text replacement that keeps formatting, block insertion.
/// The card shows the document as Markdown before and after, so the diff shows exactly the changed paragraphs.
/// </summary>
internal sealed class WordChanges(DocumentFiles files)
{
    public DocumentPlan Create(DocumentChange change, DocumentPath target)
    {
        var markdown = Required(change.Markdown, "markdown", change.Action);
        var bytes = DocxWriter.Create(MarkdownReader.Read(markdown));
        var before = files.Exists(target) ? TryText(files.Read(target)) : null;
        return new DocumentPlan(target, bytes, Of(target, before, Text(bytes), Strings.ApprovalWordCreate), Format(Strings.DocumentCreated, target.Relative));
    }

    public DocumentPlan Replace(DocumentChange change, DocumentPath target)
    {
        var find = Required(change.Find, "find", change.Action);
        var replacement = change.Replace ?? string.Empty;
        var old = files.Read(target);
        var (bytes, count) = ReplaceUnmarked(old, find, replacement, change.All);
        return new DocumentPlan(target, bytes, Of(target, Text(old), Text(bytes), Strings.ApprovalWordEdit), Format(Strings.TextReplaced, count, target.Relative));
    }

    public DocumentPlan Insert(DocumentChange change, DocumentPath target)
    {
        var content = MarkdownReader.Read(Required(change.Markdown, "markdown", change.Action));
        var old = files.Read(target);
        var after = string.IsNullOrEmpty(change.After) ? null : change.After;
        var bytes = DocxEditor.Insert(old, content, after);
        return new DocumentPlan(target, bytes, Of(target, Text(old), Text(bytes), Strings.ApprovalWordEdit), Format(Strings.BlocksInserted, content.Blocks.Length, target.Relative));
    }

    // The model often copies text with Markdown markup from the document ("**Total**"): retry without it.
    private static (byte[] Bytes, int Count) ReplaceUnmarked(byte[] bytes, string find, string replacement, bool all)
    {
        try
        {
            return DocxEditor.Replace(bytes, find, replacement, all);
        }
        catch (AgentToolException) when (Unmarked(find) is var plain && plain != find && plain.Length > 0)
        {
            return DocxEditor.Replace(bytes, plain, Unmarked(replacement), all);
        }
    }

    private static string Unmarked(string text) => text.Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("~~", string.Empty, StringComparison.Ordinal)
        .Replace("`", string.Empty, StringComparison.Ordinal);

    private static string Text(byte[] bytes) => MarkdownWriter.Write(DocxReader.Read(bytes));

    // Previous file for the card: a damaged one shows as empty since it gets replaced anyway.
    private static string TryText(byte[] bytes)
    {
        try
        {
            return Text(bytes);
        }
        catch (Exception exception) when (DocumentErrors.IsReadFailure(exception))
        {
            return string.Empty;
        }
    }
}
