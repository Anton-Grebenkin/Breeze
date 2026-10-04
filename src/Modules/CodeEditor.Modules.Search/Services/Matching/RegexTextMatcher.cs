using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>Regex matcher; empty matches (<c>^</c>, <c>a*</c>) are skipped, as in VS Code.</summary>
internal sealed class RegexTextMatcher(Regex regex) : TextMatcher
{
    public override bool TryFind(string text, int start, out int index, out int length)
    {
        for (var match = regex.Match(text, start); match.Success; match = match.NextMatch())
        {
            if (match.Length > 0)
            {
                index = match.Index;
                length = match.Length;
                return true;
            }
        }

        index = -1;
        length = 0;
        return false;
    }
}
