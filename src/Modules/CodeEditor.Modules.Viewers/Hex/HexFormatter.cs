using System.Globalization;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// A 16-byte hex dump row, as in HxD and VS Code Hex Editor: offset, two-digit bytes with a gap after the eighth, bytes
/// as ASCII characters. A short last row is padded with spaces so the character column stays aligned; non-printable
/// bytes are dots.
/// </summary>
/// <remarks>O(16) per row, one allocation per string.</remarks>
public static class HexFormatter
{
    public const int BytesPerRow = 16;

    /// <summary>Row bytes: "XX " per byte, no trailing space but an extra one in the middle; 48 characters.</summary>
    public const int HexWidth = BytesPerRow * CharsPerByte;

    /// <summary>Minimum offset digits, enough for files up to 4 GB.</summary>
    public const int MinOffsetDigits = 8;

    private const int CharsPerByte = 3;
    private const int HalfRow = BytesPerRow / 2;
    private const int BitsPerDigit = 4;
    private const int LowDigitMask = 0xF;
    private const int BitsPerOffset = 64;
    private const string Digits = "0123456789ABCDEF";
    private const char NonPrintable = '.';
    private const byte FirstPrintable = 0x20;
    private const byte LastPrintable = 0x7E;

    // "X0".."X16", built once: Offset runs for every row the list shows.
    private static readonly string[] OffsetFormats = [.. Enumerable.Range(0, (BitsPerOffset / BitsPerDigit) + 1).Select(OffsetFormat)];

    /// <summary>The byte column header: byte numbers within a row.</summary>
    public static string BytesHeader { get; } = Hex([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]);

    /// <summary>How many hex digits the offsets of a file of this length need (at least eight).</summary>
    public static int OffsetDigits(long length)
    {
        var last = (ulong)Math.Max(0, length - 1);
        var bits = BitsPerOffset - (int)ulong.LeadingZeroCount(last);
        return Math.Max(MinOffsetDigits, (bits + BitsPerDigit - 1) / BitsPerDigit);
    }

    public static string Offset(long offset, int digits) =>
        offset.ToString((uint)digits < OffsetFormats.Length ? OffsetFormats[digits] : OffsetFormat(digits), CultureInfo.InvariantCulture);

    /// <summary>Row bytes (up to 16) as hex pairs, padded with spaces to <see cref="HexWidth"/>.</summary>
    public static string Hex(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bytes.Length, BytesPerRow, nameof(bytes));
        Span<char> text = stackalloc char[HexWidth];
        text.Fill(' ');
        for (var index = 0; index < bytes.Length; index++)
        {
            var column = HexColumn(index);
            text[column] = Digits[bytes[index] >> BitsPerDigit];
            text[column + 1] = Digits[bytes[index] & LowDigitMask];
        }

        return new string(text);
    }

    /// <summary>Row bytes as ASCII characters; non-printable bytes are dots.</summary>
    public static string Text(ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bytes.Length, BytesPerRow, nameof(bytes));
        Span<char> text = stackalloc char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            text[index] = value is >= FirstPrintable and <= LastPrintable ? (char)value : NonPrintable;
        }

        return new string(text);
    }

    /// <summary>The column in the hex string where byte <paramref name="index"/> (0-15) starts.</summary>
    public static int HexColumn(int index) => (index * CharsPerByte) + (index >= HalfRow ? 1 : 0);

    private static string OffsetFormat(int digits) => "X" + digits.ToString(CultureInfo.InvariantCulture);
}
