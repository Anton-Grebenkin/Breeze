using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Formats;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Resources;
using CodeEditor.Modules.Viewers.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Read-only hex view of a binary file (ADR 0037): 16-byte rows of offset, bytes and ASCII characters. Opening reads
/// only the file length, so a multi-gigabyte file opens at once; bytes are read in pages while scrolling
/// (<see cref="HexRows"/>, <see cref="HexPages"/>). Go to offset selects the row and marks the byte.
/// </summary>
public sealed partial class HexViewerViewModel : ViewerViewModel
{
    private readonly HexPages _pages;

    public HexViewerViewModel(string filePath, ViewerContext context)
        : base(filePath, ViewerKind.Binary, context)
    {
        _pages = new HexPages(filePath, context.Bytes, context.Dispatcher);
        _pages.Failed += OnPageFailed;
        Rows = new HexRows(_pages, Strings.HexOffsetHeader);
    }

    /// <summary>Rows for the virtualized list; empty until the file length is read.</summary>
    public HexRows Rows { get; }

    public string BytesHeader => HexFormatter.BytesHeader;

    public string TextHeader => Strings.HexTextHeader;

    /// <summary>The selected row, or -1.</summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>Go to offset reached a row: the view scrolls to it.</summary>
    public event EventHandler<int>? ScrollRequested;

    /// <summary>Selects the row with the offset, marks the byte and requests a scroll to it.</summary>
    /// <returns><c>false</c> if the offset is outside the file.</returns>
    public bool GoTo(long offset)
    {
        if (offset < 0 || offset >= Rows.Length)
        {
            return false;
        }

        var row = HexRows.RowOf(offset);
        if (row >= Rows.Count)
        {
            return false;
        }

        Rebuild(() => Rows.Mark(offset));
        SelectedIndex = row;
        ScrollRequested?.Invoke(this, row);
        return true;
    }

    protected override object Read(CancellationToken cancellationToken) => Context.Bytes.GetLength(FilePath);

    protected override void Show(object content)
    {
        var length = (long)content;
        var selected = SelectedIndex;
        Rebuild(() => Rows.Reload(length));
        SelectedIndex = selected < Rows.Count ? selected : -1;
        Summary = ByteSizes.FormatExact(length);
        Note = Rows.IsTruncated ? Strings.HexTruncated : null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pages.Failed -= OnPageFailed;
            _pages.Dispose();
        }

        base.Dispose(disposing);
    }

    // Rebuilds the list with no selection: restoring a selection after a reset makes WPF enumerate every row, and a
    // multi-gigabyte file has hundreds of millions. The caller sets the selection again after the reset.
    private void Rebuild(Action reset)
    {
        SelectedIndex = -1;
        reset();
    }

    // The toolbar button runs the same command as Ctrl+G and the tab menu item: offset input in the palette.
    [RelayCommand]
    private async Task GoToOffsetAsync() => await Context.Commands.ExecuteAsync(ViewerCommands.GoToOffsetId);

    private void OnPageFailed(object? sender, string message) => ShowError(Format(Strings.ViewerLoadFailed, message));
}
