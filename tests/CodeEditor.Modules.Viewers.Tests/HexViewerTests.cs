using CodeEditor.Modules.Viewers.Commands;
using CodeEditor.Modules.Viewers.Hex;
using CodeEditor.Modules.Viewers.Tests.Infrastructure;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Testing;
using static CodeEditor.Modules.Viewers.Tests.Infrastructure.ViewersFixture;

namespace CodeEditor.Modules.Viewers.Tests;

/// <summary>
/// Hex viewer: opening reads only the length, the file size is in the info, go to offset from the palette selects the
/// row and marks the byte, a page read error is visible.
/// </summary>
public sealed class HexViewerTests : IDisposable
{
    private readonly QueueDispatcher _dispatcher = new();
    private readonly ViewersFixture _fixture;

    public HexViewerTests() => _fixture = new ViewersFixture(_dispatcher);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Opening_ReadsOnlyTheLength()
    {
        _fixture.Bytes.AddGenerated(PathOf("disk.img"), 3L * 1024 * 1024 * 1024);
        using var viewer = Open("disk.img");

        await viewer.EnsureLoadedAsync();

        Assert.Equal(3L * 1024 * 1024 * 1024, viewer.Rows.Length);
        Assert.Equal("3 ГБ (3 221 225 472 байта)", viewer.Summary);
        Assert.Empty(_fixture.Bytes.Reads);
    }

    [Fact]
    public async Task VisibleRow_ShowsTheBytes()
    {
        _fixture.Bytes.Add(PathOf("app.exe"), [0x4D, 0x5A, 0x90, 0x00]);
        using var viewer = Open("app.exe");
        await viewer.EnsureLoadedAsync();

        var row = viewer.Rows[0];
        await _dispatcher.RunNextAsync();

        Assert.Equal("00000000", row.OffsetText);
        Assert.StartsWith("4D 5A 90 00", row.Hex, StringComparison.Ordinal);
        Assert.Equal("MZ..", row.Text);
    }

    [Fact]
    public async Task GoToOffset_SelectsTheRow_MarksTheByte_AndAsksToScroll()
    {
        _fixture.Bytes.AddGenerated(PathOf("data.bin"), 0x10000);
        using var viewer = Open("data.bin");
        await viewer.EnsureLoadedAsync();
        int? scrolled = null;
        viewer.ScrollRequested += (_, row) => scrolled = row;

        var moved = viewer.GoTo(0x1F43);

        Assert.True(moved);
        Assert.Equal((0x1F4, 0x1F4), (viewer.SelectedIndex, scrolled));
        Assert.Equal(3, viewer.Rows[0x1F4].Marked);
        Assert.False(viewer.GoTo(0x10000));
        Assert.False(viewer.GoTo(-1));
    }

    // Restoring the selection after a list reset makes WPF walk every row: reset without a selection, then restore it.
    [Fact]
    public async Task ListIsRebuilt_WithoutASelection_AndTheSelectionComesBack()
    {
        _fixture.Bytes.AddGenerated(PathOf("data.bin"), 0x10000);
        using var viewer = Open("data.bin");
        await viewer.EnsureLoadedAsync();
        viewer.GoTo(0x100);
        var selectedAtReset = new List<int>();
        viewer.Rows.CollectionChanged += (_, _) => selectedAtReset.Add(viewer.SelectedIndex);

        viewer.GoTo(0x2000);
        await viewer.RefreshAsync();

        Assert.Equal([-1, -1], selectedAtReset);
        Assert.Equal(0x200, viewer.SelectedIndex);
    }

    [Fact]
    public async Task Palette_OffersBothReadings_WithinTheFile()
    {
        _fixture.Bytes.AddGenerated(PathOf("data.bin"), 0x10000);
        using var viewer = Open("data.bin");
        await viewer.EnsureLoadedAsync();
        var quickPick = new FakeQuickPick();
        quickPick.Show(new OffsetQuickOpenProvider(viewer));

        var both = quickPick.Shown!.Filter("1000");
        var outside = quickPick.Shown.Filter("0x20000");
        await quickPick.PickAsync("1000");

        Assert.Equal(["Перейти к 0x1000 (4096)", "Перейти к 1000 (0x3E8)"], both.Select(item => item.Title));
        Assert.Equal(["шестнадцатеричное", "десятичное"], both.Select(item => item.Detail));
        Assert.Empty(outside);
        Assert.Equal("Введите смещение от 0 до 0xFFFF.", quickPick.Shown.EmptyText);
        Assert.Equal(0x100, viewer.SelectedIndex);
    }

    [Fact]
    public async Task ToolbarButton_RunsTheGoToOffsetCommand()
    {
        _fixture.Bytes.AddGenerated(PathOf("data.bin"), 16);
        using var viewer = Open("data.bin");

        await viewer.GoToOffsetCommand.ExecuteAsync(null);

        Assert.Contains((ViewerCommands.GoToOffsetId, (object?)null), _fixture.Commands.Executed);
    }

    [Fact]
    public async Task UnreadablePage_ShowsAnError()
    {
        _fixture.Bytes.AddGenerated(PathOf("locked.bin"), 100);
        using var viewer = Open("locked.bin");
        await viewer.EnsureLoadedAsync();

        _fixture.Bytes.Failure = new IOException("Файл занят другим процессом.");
        _ = viewer.Rows[0];
        await _dispatcher.RunNextAsync();

        Assert.Equal("Файл не показать: Файл занят другим процессом.", viewer.Error);
    }

    [Fact]
    public async Task FileOverThirtyTwoGigabytes_IsExplained()
    {
        _fixture.Bytes.AddGenerated(PathOf("huge.img"), 40L * 1024 * 1024 * 1024);
        using var viewer = Open("huge.img");

        await viewer.EnsureLoadedAsync();

        Assert.Equal("Показаны только первые 32 ГБ файла.", viewer.Note);
    }

    private HexViewerViewModel Open(string name) => new(PathOf(name), _fixture.Context);
}
