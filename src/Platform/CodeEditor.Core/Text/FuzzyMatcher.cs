using System.Buffers;

namespace CodeEditor.Core.Text;

/// <summary>
/// Fuzzy matching for the command palette and quick open: pattern characters must appear in the candidate in order,
/// not necessarily adjacent. Case-insensitive.
/// </summary>
/// <remarks>
/// Three linear passes over the candidate, O(n), without heap allocations:
/// <list type="number">
/// <item>greedy: the first matching occurrence of each character;</item>
/// <item>boundaries: a character counts only at a word start, a CamelCase transition or right after the previous
/// match ("cm" matches <b>C</b>ommand<b>M</b>anager);</item>
/// <item>contiguous: the whole pattern as a substring ("rest" in "<b>Rest</b>art") gets a bonus, so it ranks above
/// a match spread over word starts ("<b>Re</b>build and <b>St</b>art"), as in VS Code.</item>
/// </list>
/// The best score wins. Not an exhaustive search, but good enough for ranking lists.
/// </remarks>
public static class FuzzyMatcher
{
    private const int MatchScore = 1;
    private const int ConsecutiveBonus = 5;
    private const int WordStartBonus = 8;
    private const int CamelCaseBonus = 6;
    private const int ExactCaseBonus = 1;
    private const int MaxLeadingGapPenalty = 3;

    /// <summary>Bonus for the whole pattern matching contiguously.</summary>
    private const int ContiguousBonus = 10;

    /// <summary>Extra candidate characters per penalty point: shorter candidates rank higher.</summary>
    private const int UnmatchedCharsPerPenaltyPoint = 8;

    /// <summary>Longer patterns get only the greedy pass, since the index buffer lives on the stack.</summary>
    private const int MaxBoundaryPatternLength = 64;

    private static readonly SearchValues<char> WordSeparators = SearchValues.Create(" /\\._-:");

    /// <summary>Checks for a match and computes its score: higher is better.</summary>
    public static bool TryMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> candidate, out int score) =>
        TryMatch(pattern, candidate, [], out score);

    /// <summary>
    /// Same as <see cref="TryMatch(ReadOnlySpan{char}, ReadOnlySpan{char}, out int)"/>, and also records the matched
    /// character positions for highlighting.
    /// </summary>
    /// <param name="matchedIndices">Empty, or at least as long as the pattern.</param>
    public static bool TryMatch(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> candidate,
        Span<int> matchedIndices,
        out int score)
    {
        score = 0;
        if (!matchedIndices.IsEmpty && matchedIndices.Length < pattern.Length)
        {
            throw new ArgumentException("The index buffer is shorter than the pattern.", nameof(matchedIndices));
        }

        if (pattern.IsEmpty)
        {
            return true;
        }

        if (pattern.Length > candidate.Length || !Scan(pattern, candidate, matchedIndices, boundariesOnly: false, out score))
        {
            return false;
        }

        if (pattern.Length > MaxBoundaryPatternLength)
        {
            return true;
        }

        Span<int> boundaryIndices = stackalloc int[pattern.Length];
        if (Scan(pattern, candidate, boundaryIndices, boundariesOnly: true, out var boundaryScore) && boundaryScore > score)
        {
            score = boundaryScore;
            if (!matchedIndices.IsEmpty)
            {
                boundaryIndices.CopyTo(matchedIndices);
            }
        }

        PreferContiguous(pattern, candidate, matchedIndices, ref score);

        return true;
    }

    private static bool Scan(
        ReadOnlySpan<char> pattern,
        ReadOnlySpan<char> candidate,
        Span<int> matchedIndices,
        bool boundariesOnly,
        out int score)
    {
        score = 0;
        var patternIndex = 0;
        var previousMatch = -2;
        var firstMatch = -1;
        var total = 0;

        for (var i = 0; i < candidate.Length && patternIndex < pattern.Length; i++)
        {
            var expected = pattern[patternIndex];
            if (!EqualsIgnoreCase(candidate[i], expected))
            {
                continue;
            }

            var isConsecutive = i == previousMatch + 1;
            var boundaryBonus = BoundaryBonus(candidate, i);
            if (boundariesOnly && boundaryBonus == 0 && !isConsecutive)
            {
                continue;
            }

            total += MatchScore + boundaryBonus
                + (isConsecutive ? ConsecutiveBonus : 0)
                + (candidate[i] == expected ? ExactCaseBonus : 0);

            if (!matchedIndices.IsEmpty)
            {
                matchedIndices[patternIndex] = i;
            }

            if (firstMatch < 0)
            {
                firstMatch = i;
            }

            previousMatch = i;
            patternIndex++;
        }

        if (patternIndex < pattern.Length)
        {
            return false;
        }

        total -= Math.Min(firstMatch, MaxLeadingGapPenalty);
        total -= (candidate.Length - pattern.Length) / UnmatchedCharsPerPenaltyPoint;
        score = total;
        return true;
    }

    // The whole pattern as a substring: scored with a bonus and highlighted as one run.
    private static void PreferContiguous(ReadOnlySpan<char> pattern, ReadOnlySpan<char> candidate, Span<int> matchedIndices, ref int score)
    {
        var start = candidate.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return;
        }

        var total = ContiguousBonus;
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            var index = start + offset;
            total += MatchScore + BoundaryBonus(candidate, index)
                + (offset > 0 ? ConsecutiveBonus : 0)
                + (candidate[index] == pattern[offset] ? ExactCaseBonus : 0);
        }

        total -= Math.Min(start, MaxLeadingGapPenalty) + (candidate.Length - pattern.Length) / UnmatchedCharsPerPenaltyPoint;
        if (total <= score)
        {
            return;
        }

        score = total;
        for (var offset = 0; offset < pattern.Length && !matchedIndices.IsEmpty; offset++)
        {
            matchedIndices[offset] = start + offset;
        }
    }

    /// <summary>Bonus for a word start or a CamelCase transition; 0 inside a word.</summary>
    private static int BoundaryBonus(ReadOnlySpan<char> candidate, int index)
    {
        if (index == 0 || WordSeparators.Contains(candidate[index - 1]))
        {
            return WordStartBonus;
        }

        return char.IsUpper(candidate[index]) && char.IsLower(candidate[index - 1]) ? CamelCaseBonus : 0;
    }

    private static bool EqualsIgnoreCase(char left, char right) =>
        left == right || char.ToLowerInvariant(left) == char.ToLowerInvariant(right);
}
