using System.Text;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Formats;
using CodeEditor.Modules.Documents.Formats.Pdf;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using static CodeEditor.Modules.Documents.Services.Changes.ChangePreviews;

namespace CodeEditor.Modules.Documents.Services.Changes;

/// <summary>
/// PDF for <c>document_change</c>: creation from Markdown and building a new file from pages of others (merge and
/// extract). The create card shows the Markdown; the combine card shows which pages of which files go in.
/// </summary>
internal sealed class PdfChanges(DocumentFiles files)
{
    public DocumentPlan Create(DocumentChange change, DocumentPath target)
    {
        var markdown = Required(change.Markdown, "markdown", change.Action);
        byte[] bytes;
        try
        {
            bytes = PdfWriter.Create(MarkdownReader.Read(markdown));
        }
        catch (InvalidOperationException exception)
        {
            throw new AgentToolException(exception.Message);
        }

        var before = files.Exists(target) ? string.Empty : null;
        var pages = PdfPageTools.PageCount(bytes);
        return new DocumentPlan(target, bytes, Of(target, before, markdown, Strings.ApprovalPdfCreate), Format(Strings.PdfCreated, target.Relative, pages));
    }

    public DocumentPlan Merge(DocumentChange change, DocumentPath target)
    {
        if (change.Sources.Count == 0)
        {
            throw new AgentToolException(Format(Strings.ArgumentRequired, change.Action, "sources"));
        }

        return Combine(target, change.Sources.Select(source => (source.Path, source.Pages)).ToList());
    }

    public DocumentPlan Extract(DocumentChange change, DocumentPath target) =>
        Combine(target, [(Required(change.Source, "source", change.Action), Required(change.Pages, "pages", change.Action))]);

    private DocumentPlan Combine(DocumentPath target, IReadOnlyList<(string Path, string? Pages)> sources)
    {
        var inputs = new List<(byte[] Bytes, IReadOnlyList<int> Pages)>();
        var description = new StringBuilder();
        foreach (var (path, pages) in sources)
        {
            var source = files.Resolve(path, forWriting: false);
            if (source.Kind != DocumentKind.Pdf)
            {
                throw new AgentToolException(Format(Strings.NotPdfSource, source.Relative));
            }

            var bytes = files.Read(source);
            var count = PdfPageTools.PageCount(bytes);
            if (!PageSelection.TryParse(pages, count, out var selected))
            {
                throw new AgentToolException(Format(Strings.InvalidPages, pages, count));
            }

            inputs.Add((bytes, selected));
            description.Append(Format(Strings.SourcePages, source.Relative, PageSelection.Describe(selected), selected.Count)).Append('\n');
        }

        var total = inputs.Sum(input => input.Pages.Count);
        description.Append(Format(Strings.TotalPages, total));
        var before = files.Exists(target) ? string.Empty : null;
        return new DocumentPlan(target, PdfPageTools.Combine(inputs), Of(target, before, description.ToString(), Strings.ApprovalPdfCombine),
            Format(Strings.PdfCreated, target.Relative, total));
    }
}
