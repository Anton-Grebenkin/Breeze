using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Edits a Word document without losing formatting: the rest of the package (images, headers and footers, comments,
/// markup the editor does not understand) stays untouched. Text is replaced in body, table, header and footer
/// paragraphs; blocks are inserted after the paragraph or table with the given text, or at the end.
/// </summary>
internal static class DocxEditor
{
    /// <returns>The new document bytes and the number of replacements.</returns>
    /// <exception cref="AgentToolException">
    /// The text is not found, or is found more than once without <paramref name="all"/>.
    /// </exception>
    public static (byte[] Bytes, int Count) Replace(byte[] bytes, string find, string replacement, bool all) => Edit(bytes, main =>
    {
        var paragraphs = Paragraphs(main).Select(paragraph => new ParagraphText(paragraph)).ToList();
        var count = paragraphs.Sum(paragraph => paragraph.Count(find));
        if (count == 0)
        {
            throw new AgentToolException(Format(Strings.TextNotFound, find));
        }

        if (count > 1 && !all)
        {
            throw new AgentToolException(Format(Strings.TextFoundManyTimes, find, count));
        }

        return paragraphs.Sum(paragraph => paragraph.Replace(find, replacement));
    });

    /// <summary>
    /// Inserts blocks after the body element whose text contains <paramref name="after"/>, or at the end without it.
    /// </summary>
    /// <exception cref="AgentToolException">The anchor is not found or is not unique.</exception>
    public static byte[] Insert(byte[] bytes, RichDocument content, string? after) => Edit(bytes, main =>
    {
        var body = main.Document?.Body ?? throw OpenXmlFiles.NotDocument();
        var anchor = after is null ? null : Anchor(body, after);
        var elements = new WordBlocks(main, WordStyles.Ensure(main), new WordLists(main)).Convert(content).ToList();
        if (anchor is null)
        {
            // Append before the last section properties: they must stay the last child of the body.
            var section = body.Elements<SectionProperties>().LastOrDefault();
            foreach (var element in elements)
            {
                if (section is null)
                {
                    body.Append(element);
                }
                else
                {
                    section.InsertBeforeSelf(element);
                }
            }

            return elements.Count;
        }

        foreach (var element in elements)
        {
            anchor = anchor.InsertAfterSelf(element);
        }

        return elements.Count;
    }).Bytes;

    private static OpenXmlElement Anchor(Body body, string after)
    {
        var matches = body.ChildElements
            .Where(element => element is Paragraph or Table or SdtBlock)
            .Where(element => string.Concat(element.Descendants<Text>().Select(text => text.Text)).Contains(after, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        return matches.Count switch
        {
            0 => throw new AgentToolException(Format(Strings.TextNotFound, after)),
            > 1 => throw new AgentToolException(Format(Strings.AnchorNotUnique, after)),
            _ => matches[0],
        };
    }

    // Body paragraphs (including tables and text boxes), headers and footers. The text box fallback copy for old Word
    // versions (mc:Fallback) repeats its text and is skipped, otherwise each match in a text box would count twice.
    private static IEnumerable<Paragraph> Paragraphs(MainDocumentPart main) =>
        (main.Document?.Body?.Descendants<Paragraph>() ?? [])
            .Concat(main.HeaderParts.SelectMany(header => header.Header?.Descendants<Paragraph>() ?? []))
            .Concat(main.FooterParts.SelectMany(footer => footer.Footer?.Descendants<Paragraph>() ?? []))
            .Where(paragraph => !paragraph.Ancestors<AlternateContentFallback>().Any());

    private static (byte[] Bytes, int Count) Edit(byte[] bytes, Func<MainDocumentPart, int> change)
    {
        using var stream = OpenXmlFiles.Editable(bytes);
        int count;
        using (var document = OpenXmlFiles.OpenWord(stream, editable: true))
        {
            count = change(document.MainDocumentPart ?? throw OpenXmlFiles.NotDocument());
        }

        return (stream.ToArray(), count);
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
