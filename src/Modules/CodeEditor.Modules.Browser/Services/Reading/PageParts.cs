namespace CodeEditor.Modules.Browser.Services.Reading;

/// <summary>
/// A long page text handed to the model in parts of about <see cref="Size"/> characters, cut at line ends so no
/// snapshot line or Markdown paragraph is split. A line longer than a part is cut by characters. O(n).
/// </summary>
public static class PageParts
{
    public const int Size = 12_000;

    /// <param name="part">1-based; out of range is brought to the first or the last part.</param>
    /// <returns>The part, its number and how many parts there are.</returns>
    public static (string Text, int Part, int Count) Take(string text, int? part)
    {
        ArgumentNullException.ThrowIfNull(text);
        var starts = Starts(text);
        var index = Math.Clamp(part ?? 1, 1, starts.Count) - 1;
        var end = index + 1 < starts.Count ? starts[index + 1] : text.Length;
        return (text[starts[index]..end].TrimEnd('\n'), index + 1, starts.Count);
    }

    private static List<int> Starts(string text)
    {
        var starts = new List<int> { 0 };
        var start = 0;
        while (text.Length - start > Size)
        {
            // The last line end inside the part; a part without one is a single long line.
            var cut = text.LastIndexOf('\n', start + Size - 1, Size);
            start = cut > start ? cut + 1 : start + Size;
            starts.Add(start);
        }

        return starts;
    }
}
