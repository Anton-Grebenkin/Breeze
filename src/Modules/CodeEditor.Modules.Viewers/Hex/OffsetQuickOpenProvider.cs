using System.Globalization;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Modules.Viewers.Hex;

/// <summary>
/// Offset input in the palette: go to offset in the hex view, like ":" for a text line. The items are the readings of
/// the entered number that fall inside the file (<see cref="OffsetInput"/>): "0x1F40 (8000)" and "8000 (0x1F40)".
/// </summary>
public sealed class OffsetQuickOpenProvider(HexViewerViewModel viewer) : IQuickOpenProvider
{
    public string Prefix => string.Empty;

    public string Placeholder => Strings.GoToOffsetPlaceholder;

    public string EmptyText => viewer.Rows.Length == 0
        ? Strings.GoToOffsetEmptyFile
        : Format(Strings.GoToOffsetRange, Hex(viewer.Rows.Length - 1));

    public void Prepare()
    {
    }

    public IReadOnlyList<PaletteItem> Filter(string text) =>
        [.. OffsetInput.Parse(text).Where(candidate => candidate.Offset < viewer.Rows.Length).Select(Item)];

    public Task AcceptAsync(PaletteItem item, string text)
    {
        ArgumentNullException.ThrowIfNull(item);
        viewer.GoTo(long.Parse(item.Id, CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    private static PaletteItem Item(OffsetCandidate candidate)
    {
        var decimalText = candidate.Offset.ToString(CultureInfo.InvariantCulture);
        var title = candidate.IsHexadecimal
            ? Format(Strings.GoToOffsetItem, Hex(candidate.Offset), decimalText)
            : Format(Strings.GoToOffsetItem, decimalText, Hex(candidate.Offset));
        return new PaletteItem(decimalText, title, null, [], false)
        {
            Detail = candidate.IsHexadecimal ? Strings.OffsetHexadecimal : Strings.OffsetDecimal,
        };
    }

    private static string Hex(long offset) => "0x" + offset.ToString("X", CultureInfo.InvariantCulture);

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
