using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Search.Resources;

namespace CodeEditor.Modules.Search.Services.Matching;

/// <summary>
/// Finds query matches in text: literal text via vectorized <c>IndexOf</c>, regex via <see cref="Regex"/> with a
/// timeout. Thread-safe: one instance serves all files of a search.
/// </summary>
public abstract class TextMatcher
{
    /// <summary>Per-file regex timeout, so catastrophic backtracking can't hang the search.</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <exception cref="ArgumentException">Invalid regex; the message is user-facing.</exception>
    public static TextMatcher Create(TextSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.Pattern);

        if (!options.UseRegex)
        {
            return new PlainTextMatcher(options.Pattern, options.MatchCase, options.WholeWord);
        }

        var pattern = options.WholeWord ? $@"\b(?:{options.Pattern})\b" : options.Pattern;
        var regexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new RegexTextMatcher(new Regex(pattern, regexOptions, RegexTimeout));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, Strings.InvalidRegex, exception.Message), nameof(options), exception);
        }
    }

    /// <summary>Finds the next non-empty match starting at <paramref name="start"/>.</summary>
    /// <exception cref="RegexMatchTimeoutException">The regex exceeded <see cref="RegexTimeout"/>.</exception>
    public abstract bool TryFind(string text, int start, out int index, out int length);
}
