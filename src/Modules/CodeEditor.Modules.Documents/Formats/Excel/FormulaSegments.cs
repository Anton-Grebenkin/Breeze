namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Splits formula text into quoted strings (a doubled quote is a literal quote), sheet names in apostrophes, brackets
/// (nested, with apostrophe escapes, as in table references "Table1[[#This Row],[Q1]]") and the rest. References are
/// searched only in the rest. O(n) in the formula length.
/// </summary>
internal static class FormulaSegments
{
    public static IEnumerable<(string Text, FormulaSegmentKind Kind)> Split(string formula)
    {
        var start = 0;
        var index = 0;
        while (index < formula.Length)
        {
            var character = formula[index];
            if (character is not ('"' or '\'' or '['))
            {
                index++;
                continue;
            }

            if (index > start)
            {
                yield return (formula[start..index], FormulaSegmentKind.Plain);
            }

            var (end, kind) = character switch
            {
                '"' => (ClosingQuote(formula, index, '"'), FormulaSegmentKind.String),
                '\'' => (ClosingQuote(formula, index, '\''), FormulaSegmentKind.SheetName),
                _ => (ClosingBracket(formula, index), FormulaSegmentKind.Bracket),
            };
            yield return (formula[index..end], kind);
            start = index = end;
        }

        if (start < formula.Length)
        {
            yield return (formula[start..], FormulaSegmentKind.Plain);
        }
    }

    // A doubled quote inside is a literal quote, not the end.
    private static int ClosingQuote(string formula, int open, char quote)
    {
        var index = open + 1;
        while (index < formula.Length)
        {
            if (formula[index] != quote)
            {
                index++;
                continue;
            }

            if (index + 1 < formula.Length && formula[index + 1] == quote)
            {
                index += 2;
                continue;
            }

            return index + 1;
        }

        return formula.Length;
    }

    // Brackets nest; an apostrophe escapes the next character (column name "Q'[1']").
    private static int ClosingBracket(string formula, int open)
    {
        var depth = 0;
        for (var index = open; index < formula.Length; index++)
        {
            switch (formula[index])
            {
                case '\'':
                    index++;
                    break;
                case '[':
                    depth++;
                    break;
                case ']' when --depth == 0:
                    return index + 1;
                default:
                    break;
            }
        }

        return formula.Length;
    }
}
