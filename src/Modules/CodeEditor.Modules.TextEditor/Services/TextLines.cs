namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>Splits text into lines without copying; handles <c>\n</c> and <c>\r\n</c>. O(n).</summary>
public static class TextLines
{
    public static IReadOnlyList<TextLine> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<TextLine>();
        var start = 0;
        for (var index = text.IndexOf('\n', start); index >= 0; index = text.IndexOf('\n', start))
        {
            var end = index > start && text[index - 1] == '\r' ? index - 1 : index;
            lines.Add(new TextLine(start, end, index + 1));
            start = index + 1;
        }

        lines.Add(new TextLine(start, text.Length, text.Length));
        return lines;
    }
}
