using System.Globalization;

namespace CodeEditor.Modules.Documents.Model;

/// <summary>Page or slide selection: "3", "2-5", "1,4-6", "5-" (fifth to the end); empty means all in order.</summary>
public static class PageSelection
{
    private static readonly char[] Dashes = ['-', '–', '—'];

    /// <param name="count">Number of pages in the document: page numbers must be within 1…count.</param>
    /// <returns><c>false</c> if the text can't be parsed or a number is outside the document.</returns>
    public static bool TryParse(string? text, int count, out IReadOnlyList<int> pages)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            pages = [.. Enumerable.Range(1, count)];
            return true;
        }

        var selected = new List<int>();
        var seen = new HashSet<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryRange(part, count, out var first, out var last))
            {
                pages = [];
                return false;
            }

            for (var page = first; page <= last; page++)
            {
                if (seen.Add(page))
                {
                    selected.Add(page);
                }
            }
        }

        pages = selected;
        return selected.Count > 0;
    }

    /// <summary>Consecutive numbers as ranges: 1, 2, 3, 7 → "1–3, 7".</summary>
    public static string Describe(IReadOnlyList<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var parts = new List<string>();
        for (var start = 0; start < pages.Count;)
        {
            var end = start;
            while (end + 1 < pages.Count && pages[end + 1] == pages[end] + 1)
            {
                end++;
            }

            parts.Add(end == start ? Number(pages[start]) : $"{Number(pages[start])}–{Number(pages[end])}");
            start = end + 1;
        }

        return string.Join(", ", parts);
    }

    // "5" is one page, "2-5" a range, "5-" up to the end of the document.
    private static bool TryRange(string part, int count, out int first, out int last)
    {
        var dash = part.IndexOfAny(Dashes);
        var from = (dash < 0 ? part : part[..dash]).Trim();
        var to = (dash < 0 ? part : part[(dash + 1)..]).Trim();
        last = count;
        if (!TryNumber(from, out first) || (to.Length > 0 && !TryNumber(to, out last)))
        {
            return false;
        }

        return first >= 1 && first <= last && last <= count;
    }

    private static bool TryNumber(string text, out int number) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
