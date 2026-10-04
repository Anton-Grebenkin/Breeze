namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>Literal text: vectorized span <c>IndexOf</c>; whole word checks the boundaries.</summary>
internal sealed class PlainTextMatcher(string pattern, bool matchCase, bool wholeWord) : TextMatcher
{
    private readonly StringComparison _comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    public override bool TryFind(string text, int start, out int index, out int length)
    {
        length = pattern.Length;
        while (start <= text.Length - pattern.Length)
        {
            var found = text.AsSpan(start).IndexOf(pattern, _comparison);
            if (found < 0)
            {
                break;
            }

            index = start + found;
            if (!wholeWord || IsWholeWord(text, index, pattern.Length))
            {
                return true;
            }

            start = index + 1;
        }

        index = -1;
        return false;
    }

    // Word boundary like \b: neither side is a letter, digit or '_'.
    private static bool IsWholeWord(string text, int index, int length) =>
        (index == 0 || !IsWordChar(text[index - 1])) &&
        (index + length == text.Length || !IsWordChar(text[index + length]));

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
