using System.Collections.Immutable;
using System.Text;
using CodeEditor.Core.Documents;
using CodeEditor.Modules.Documents.Model;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// CSV to a sheet of text cells per RFC 4180: quoted fields may hold line breaks and doubled quotes. The delimiter is a
/// tab for .tsv, otherwise the first of tab, ';', '|' and ',' that occurs equally often outside quotes in every probed
/// line: Russian Excel writes "1,5;2,7" — more commas, but semicolons split the fields. Encoding detection matches the
/// text editor: BOM, UTF-8, otherwise ANSI. O(n) in the text length.
/// </summary>
internal static class CsvReader
{
    private const int ProbeLines = 20;
    private static readonly char[] Candidates = ['\t', ';', '|', ','];

    /// <exception cref="InvalidDataException">The file is binary.</exception>
    public static Spreadsheet Read(byte[] bytes, string fileName, int maxRows = CellAddress.MaxRow)
    {
        var text = TextFileCodec.Decode(bytes)?.Text ?? throw new InvalidDataException(Strings.CsvIsBinary);
        var delimiter = Path.GetExtension(fileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : Detect(text);
        var cells = Parse(text, delimiter, maxRows);
        return new Spreadsheet([new SpreadsheetSheet(Path.GetFileNameWithoutExtension(fileName), cells)]);
    }

    public static char Detect(string text)
    {
        var lines = CountPerLine(text);
        for (var index = 0; index < Candidates.Length && lines.Count > 0; index++)
        {
            var expected = lines[0][index];
            if (expected > 0 && lines.TrueForAll(counts => counts[index] == expected))
            {
                return Candidates[index];
            }
        }

        // None splits every line equally: take the most frequent one.
        var totals = Candidates.Select((_, index) => lines.Sum(counts => counts[index])).ToList();
        var best = totals.IndexOf(totals.Max());
        return totals[best] > 0 ? Candidates[best] : ',';
    }

    // Per-candidate counts outside quotes for each non-empty line among the first ProbeLines.
    private static List<int[]> CountPerLine(string text)
    {
        var lines = new List<int[]>();
        var current = new int[Candidates.Length];
        var (quoted, empty) = (false, true);
        foreach (var character in text)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && character == '\n')
            {
                if (!empty)
                {
                    lines.Add(current);
                }

                if (lines.Count >= ProbeLines)
                {
                    return lines;
                }

                (current, empty) = (new int[Candidates.Length], true);
                continue;
            }
            else if (!quoted && Array.IndexOf(Candidates, character) is var index and >= 0)
            {
                current[index]++;
            }

            empty &= char.IsWhiteSpace(character);
        }

        if (!empty)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static ImmutableArray<SpreadsheetCell> Parse(string text, char delimiter, int maxRows)
    {
        var cells = ImmutableArray.CreateBuilder<SpreadsheetCell>();
        var field = new StringBuilder();
        var (row, column, quoted) = (1, 1, false);
        for (var index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0; index < text.Length && row <= maxRows; index++)
        {
            var character = text[index];
            if (quoted)
            {
                // Line breaks inside a field drop the CR, as in Excel.
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    continue;
                }

                if (character != '"')
                {
                    field.Append(character);
                }
                else if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == delimiter)
            {
                Add(cells, field, row, column++);
            }
            else if (character == '\n')
            {
                Add(cells, field, row++, column);
                column = 1;
            }
            else if (character != '\r')
            {
                field.Append(character);
            }
        }

        if (row <= maxRows)
        {
            Add(cells, field, row, column);
        }

        return cells.ToImmutable();
    }

    private static void Add(ImmutableArray<SpreadsheetCell>.Builder cells, StringBuilder field, int row, int column)
    {
        if (field.Length > 0)
        {
            cells.Add(new SpreadsheetCell(row, column, CellKind.Text, field.ToString()));
            field.Clear();
        }
    }
}
