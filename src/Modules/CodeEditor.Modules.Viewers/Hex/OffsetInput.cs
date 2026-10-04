using System.Globalization;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// A go-to offset as typed in HxD and VS Code: <c>0x1F40</c> and <c>1F40h</c> are hexadecimal, and so is <c>ff</c>
/// (it has letters). Plain digits (<c>8000</c>) read both ways: the tab shows hex offsets, so the hexadecimal reading
/// comes first and the decimal second; the palette shows both.
/// </summary>
public static class OffsetInput
{
    private const string HexPrefix = "0x";
    private const string HexSuffix = "h";

    public static IReadOnlyList<OffsetCandidate> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var value = text.AsSpan().Trim();
        if (value.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Hexadecimal(value[HexPrefix.Length..]);
        }

        if (value.EndsWith(HexSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return Hexadecimal(value[..^HexSuffix.Length]);
        }

        var hexadecimal = Hexadecimal(value);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return hexadecimal;
        }

        var decimalCandidate = new OffsetCandidate(number, IsHexadecimal: false);
        return hexadecimal is [{ Offset: var same }] && same != number ? [hexadecimal[0], decimalCandidate] : [decimalCandidate];
    }

    private static OffsetCandidate[] Hexadecimal(ReadOnlySpan<char> digits) =>
        !digits.IsEmpty && long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var offset) && offset >= 0
            ? [new OffsetCandidate(offset, IsHexadecimal: true)]
            : [];
}
