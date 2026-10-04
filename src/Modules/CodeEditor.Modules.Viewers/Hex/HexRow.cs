using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// A dump row: the offset is known at once, bytes and characters once the file page is read. Rows compare by index:
/// the list creates a row anew each time it is shown, and selection and scrolling find it by equality.
/// </summary>
public sealed partial class HexRow : ObservableObject, IEquatable<HexRow>
{
    public const int NoMark = -1;

    public HexRow(int index, string offsetText, int marked)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(offsetText);
        Index = index;
        OffsetText = offsetText;
        Marked = marked;
    }

    public int Index { get; }

    public long Offset => (long)Index * HexFormatter.BytesPerRow;

    public string OffsetText { get; }

    /// <summary>The byte (0-15) reached by go to offset, or <see cref="NoMark"/>.</summary>
    public int Marked { get; }

    /// <summary>Bytes as hex pairs; empty while the page is still being read.</summary>
    [ObservableProperty]
    public partial string Hex { get; private set; } = string.Empty;

    /// <summary>Bytes as ASCII characters.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    public bool Equals(HexRow? other) => other is not null && other.Index == Index;

    public override bool Equals(object? obj) => Equals(obj as HexRow);

    public override int GetHashCode() => Index;

    public override string ToString() => $"{OffsetText}  {Hex}  {Text}";

    internal void Show(ReadOnlySpan<byte> bytes)
    {
        Hex = HexFormatter.Hex(bytes);
        Text = HexFormatter.Text(bytes);
    }
}
