using System.Globalization;
using System.Text;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Formats;

/// <summary>
/// Document blocks to Markdown for the model: "#" headings, nested lists, tables with a header first row, **bold**,
/// *italic*, ~~strikethrough~~, `code`, links. Text is not escaped: the model copies it into edit searches, and it must
/// match the document. O(n) in the text length.
/// </summary>
internal static class MarkdownWriter
{
    private const string Fence = "```";

    public static string Write(RichDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var output = new StringBuilder();
        DocumentBlock? previous = null;
        foreach (var block in document.Blocks)
        {
            Separate(output, previous, block);
            AppendBlock(output, block, previous);
            previous = block;
        }

        CloseCode(output, previous);
        return output.ToString().TrimEnd() + "\n";
    }

    // Items of one list and lines of one code block are adjacent; other blocks are separated by a blank line.
    private static void Separate(StringBuilder output, DocumentBlock? previous, DocumentBlock block)
    {
        if (previous is null)
        {
            return;
        }

        var sameList = previous.Kind == DocumentBlockKind.ListItem && block.Kind == DocumentBlockKind.ListItem;
        var sameCode = previous.Kind == DocumentBlockKind.Code && block.Kind == DocumentBlockKind.Code;
        if (sameList || sameCode)
        {
            output.Append('\n');
            return;
        }

        CloseCode(output, previous);
        output.Append("\n\n");
    }

    private static void CloseCode(StringBuilder output, DocumentBlock? previous)
    {
        if (previous?.Kind == DocumentBlockKind.Code)
        {
            output.Append('\n').Append(Fence);
        }
    }

    private static void AppendBlock(StringBuilder output, DocumentBlock block, DocumentBlock? previous)
    {
        switch (block.Kind)
        {
            case DocumentBlockKind.Heading:
                output.Append('#', Math.Clamp(block.Level, 1, 6)).Append(' ').Append(Line(block.Runs, "\n"));
                break;
            case DocumentBlockKind.ListItem:
                var indent = new string(' ', block.Level * (block.IsOrdered ? 3 : 2));
                var marker = block.IsOrdered ? block.Number.ToString(CultureInfo.InvariantCulture) + ". " : "- ";
                output.Append(indent).Append(marker).Append(Line(block.Runs, "  \n" + indent + "  "));
                break;
            case DocumentBlockKind.Table when block.Table is { } table:
                AppendTable(output, table);
                break;
            case DocumentBlockKind.Code:
                if (previous?.Kind != DocumentBlockKind.Code)
                {
                    output.Append(Fence).Append('\n');
                }

                output.Append(block.PlainText.TrimEnd('\n'));
                break;
            case DocumentBlockKind.Quote:
                output.Append("> ").Append(Line(block.Runs, "\n> "));
                break;
            case DocumentBlockKind.Rule:
                output.Append("---");
                break;
            case DocumentBlockKind.Slide:
                output.Append("## ").Append(SlideTitle(block));
                break;
            case DocumentBlockKind.Notes:
                output.Append("> ").Append(Strings.NotesLabel).Append(' ').Append(Line(block.Runs, "\n> "));
                break;
            default:
                output.Append(Line(block.Runs, "  \n"));
                break;
        }
    }

    // Edge spaces come from document indents and tabs; Markdown does not need them.
    private static string Line(IEnumerable<TextRun> runs, string lineBreak) => Inline(runs, lineBreak).Trim();

    private static string SlideTitle(DocumentBlock slide)
    {
        var number = string.Format(CultureInfo.CurrentCulture, Strings.SlideNumber, slide.Number);
        var title = Line(slide.Runs, " ");
        return title.Length == 0 ? number : number + ": " + title;
    }

    private static void AppendTable(StringBuilder output, DocumentTable table)
    {
        if (table.Rows.IsEmpty)
        {
            return;
        }

        var columns = Math.Max(table.ColumnCount, 1);
        for (var row = 0; row < table.Rows.Length; row++)
        {
            output.Append('|');
            for (var column = 0; column < columns; column++)
            {
                var cell = column < table.Rows[row].Length ? Cell(table.Rows[row][column], header: row == 0) : string.Empty;
                output.Append(' ').Append(cell).Append(" |");
            }

            output.Append('\n');
            if (row == 0)
            {
                output.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", columns))).Append('\n');
            }
        }

        output.Length--;
    }

    // A Markdown table header row already stands out: bold there is noise.
    private static string Cell(IEnumerable<TextRun> runs, bool header) =>
        Inline(header ? runs.Select(run => run with { Style = run.Style & ~TextStyle.Bold }) : runs, "<br>").Trim().Replace("|", "\\|", StringComparison.Ordinal);

    /// <summary>Runs with style markup; line breaks inside a block become <paramref name="lineBreak"/>.</summary>
    public static string Inline(IEnumerable<TextRun> runs, string lineBreak)
    {
        var output = new StringBuilder();
        foreach (var run in runs)
        {
            var text = Styled(run);
            output.Append(run.Link is { } link && !run.Has(TextStyle.Code) ? $"[{text}]({link})" : text);
        }

        return output.ToString().Replace("\n", lineBreak, StringComparison.Ordinal);
    }

    // Markup wraps the text without edge spaces: Markdown does not treat "** bold**" as emphasis.
    private static string Styled(TextRun run)
    {
        var core = run.Text.Trim(' ', '\t');
        if (core.Length == 0)
        {
            return run.Text;
        }

        var start = run.Text.IndexOf(core, StringComparison.Ordinal);
        var lead = run.Text[..start];
        var tail = run.Text[(start + core.Length)..];
        return lead + Wrap(core, run) + tail;
    }

    private static string Wrap(string core, TextRun run)
    {
        if (run.Has(TextStyle.Code))
        {
            return core.Contains('`', StringComparison.Ordinal) ? $"`` {core} ``" : $"`{core}`";
        }

        var marker = (run.Has(TextStyle.Bold) ? "**" : string.Empty) + (run.Has(TextStyle.Italic) ? "*" : string.Empty);
        var styled = marker + core + marker;
        return run.Has(TextStyle.Strike) ? "~~" + styled + "~~" : styled;
    }
}
