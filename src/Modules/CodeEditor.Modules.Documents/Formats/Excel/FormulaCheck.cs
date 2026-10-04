using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Documents.Resources;

namespace CodeEditor.Modules.Documents.Formats.Excel;

/// <summary>
/// Checks a formula before it is written. Files store formulas in English: SUM and IF, arguments separated by commas.
/// Excel would drop a formula with ';', a Russian function name or an unclosed parenthesis on open and offer to repair
/// the file, so the model learns about it right away. ';' is allowed inside array constants "{1;2}" and strings.
/// </summary>
internal static partial class FormulaCheck
{
    /// <param name="formula">The formula without the leading '='.</param>
    /// <exception cref="AgentToolException">The formula cannot be written to the file.</exception>
    public static void Validate(string formula)
    {
        if (formula.StartsWith('=') || formula.AsSpan().Count('"') % 2 != 0 || !Balanced(formula))
        {
            throw Error(Strings.FormulaUnbalanced, formula);
        }

        foreach (var (text, kind) in FormulaSegments.Split(formula))
        {
            if (kind != FormulaSegmentKind.Plain)
            {
                continue;
            }

            if (OutsideBraces(text).Contains(';', StringComparison.Ordinal))
            {
                throw Error(Strings.FormulaSemicolon, formula);
            }

            if (LocalizedFunction().IsMatch(text))
            {
                throw Error(Strings.FormulaLocalized, formula);
            }
        }
    }

    private static bool Balanced(string formula)
    {
        var depth = 0;
        foreach (var (text, kind) in FormulaSegments.Split(formula))
        {
            if (kind != FormulaSegmentKind.Plain)
            {
                continue;
            }

            foreach (var character in text)
            {
                depth += character switch { '(' => 1, ')' => -1, _ => 0 };
                if (depth < 0)
                {
                    return false;
                }
            }
        }

        return depth == 0;
    }

    // In an array constant "{1,2;3,4}" ';' separates rows, not arguments.
    private static string OutsideBraces(string text) => Braces().Replace(text, string.Empty);

    private static AgentToolException Error(string format, string formula) =>
        new(string.Format(CultureInfo.CurrentCulture, format, "=" + formula));

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Braces();

    // A Cyrillic function name before a parenthesis, e.g. the Russian SUM or IF.
    [GeneratedRegex(@"\p{IsCyrillic}[\p{L}\p{N}_.]*\(")]
    private static partial Regex LocalizedFunction();
}
