namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Re-indents the model's new text to the file's style. Models mix tabs and spaces, especially after
/// <c>read_file</c>, which separates line numbers with a tab. The model's base indent becomes the file's; nesting is
/// measured in columns (tab = 4) and rendered the file's way, with tabs or spaces.
/// </summary>
internal static class Indentation
{
    public const int TabWidth = 4;

    public static string Adapt(string newText, string modelBase, string fileBase, bool fileUsesTabs)
    {
        var shift = Width(fileBase) - Width(modelBase);
        var parts = newText.Split('\n');
        for (var i = 0; i < parts.Length; i++)
        {
            var body = parts[i].TrimStart(' ', '\t');
            if (body.Length == 0 || body == "\r")
            {
                continue;
            }

            var width = Width(parts[i].AsSpan(0, parts[i].Length - body.Length));
            parts[i] = Render(Math.Max(0, width + shift), fileUsesTabs) + body;
        }

        return string.Join('\n', parts);
    }

    private static int Width(ReadOnlySpan<char> whitespace)
    {
        var width = 0;
        foreach (var character in whitespace)
        {
            width += character == '\t' ? TabWidth : 1;
        }

        return width;
    }

    private static string Render(int width, bool tabs) =>
        tabs ? new string('\t', width / TabWidth) + new string(' ', width % TabWidth) : new string(' ', width);
}
