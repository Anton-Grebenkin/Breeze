namespace CodeEditor.Core.Text;

/// <summary>
/// Natural sort order, as in Windows Explorer: numbers inside strings compare by value (<c>file2</c> before
/// <c>file10</c>), everything else case-insensitively.
/// </summary>
/// <remarks>One allocation-free pass over both strings, O(n).</remarks>
public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        var (i, j) = (0, 0);
        while (i < x.Length && j < y.Length)
        {
            var result = char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j])
                ? CompareNumbers(x, ref i, y, ref j)
                : CompareChars(x[i++], y[j++]);

            if (result != 0)
            {
                return result;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }

    private static int CompareChars(char left, char right) =>
        char.ToUpperInvariant(left).CompareTo(char.ToUpperInvariant(right));

    /// <summary>Compares numbers of any length: by length without leading zeros, then digit by digit.</summary>
    private static int CompareNumbers(string x, ref int i, string y, ref int j)
    {
        var xStart = SkipZeros(x, ref i);
        var yStart = SkipZeros(y, ref j);
        var xDigits = x.AsSpan(i, CountDigits(x, i));
        var yDigits = y.AsSpan(j, CountDigits(y, j));
        i += xDigits.Length;
        j += yDigits.Length;

        if (xDigits.Length != yDigits.Length)
        {
            return xDigits.Length.CompareTo(yDigits.Length);
        }

        var byDigits = xDigits.SequenceCompareTo(yDigits);
        return byDigits != 0 ? byDigits : (i - xStart).CompareTo(j - yStart);
    }

    private static int SkipZeros(string text, ref int index)
    {
        var start = index;
        while (index < text.Length - 1 && text[index] == '0' && char.IsAsciiDigit(text[index + 1]))
        {
            index++;
        }

        return start;
    }

    private static int CountDigits(string text, int start)
    {
        var end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return end - start;
    }
}
