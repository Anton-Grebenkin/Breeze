using CodeEditor.Core.Storage;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.ToolWindows;

/// <summary>
/// Moving tool windows (ADR 0031): between areas and into the editor as a tab; moving back closes the tab; the content
/// is created once; the "Move to…" menu, the palette and persisting locations in the layout.
/// </summary>
public sealed class ToolWindowPlacementTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CodeEditor.PlacementTests", Guid.NewGuid().ToString("N"));
    private readonly JsonLayoutStore _store;
    private readonly LayoutFixture _fixture;
    private readonly object _content = new();
    private int _created;

    public ToolWindowPlacementTests()
    {
        _store = new JsonLayoutStore(new UserDataPaths(_folder), NullLogger<JsonLayoutStore>.Instance);
        _fixture = new LayoutFixture(_store);
        _fixture.Registry.Register(new ToolWindowDefinition("browser", "Браузер", "globe", ToolWindowLocation.Panel, () =>
        {
            _created++;
            return _content;
        }));
    }

    private WorkbenchLayout Layout => _fixture.Layout;

    public void Dispose()
    {
        _fixture.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task MovedToEditor_IsATabWithTheSameContent_AndBackClosesIt()
    {
        Layout.Panel.Show("browser");

        Layout.Move("browser", ToolWindowLocation.Editor);

        var tab = Assert.IsType<ViewTabViewModel>(Assert.Single(_fixture.Editors.Area.Tabs));
        Assert.Same(_content, tab.Editor);
        Assert.Empty(Layout.Panel.Items);
        Assert.Same(_fixture.Placement.Find("browser"), _fixture.EditorWindows.Active);

        await _fixture.Editors.Area.CloseAsync(tab);
        Assert.True(Layout.Show("browser", focus: false));
        Layout.Move("browser", ToolWindowLocation.SecondarySideBar);

        Assert.Empty(_fixture.Editors.Area.Tabs);
        Assert.Equal(("browser", true), (Layout.SecondarySideBar.Active?.Id, Layout.SecondarySideBar.IsVisible));
        Assert.Equal(1, _created);
    }

    // The tab and icon menu lists every area except the current one; choosing one moves the tool window.
    [Fact]
    public async Task MoveMenu_OffersOtherAreas_AndMoves()
    {
        var browser = _fixture.Placement.Find("browser")!;

        Assert.Equal(["Переместить в боковую панель", "Переместить в область редактора", "Переместить во вторичную боковую панель"],
            browser.MoveMenu.Select(item => item.Header));
        await browser.MoveMenu[0].Command!.ExecuteAsync(null);

        Assert.Equal(ToolWindowLocation.SideBar, browser.Location);
        Assert.Equal("browser", Layout.SideBar.Active?.Id);
    }

    [Fact]
    public void Locations_AreSavedWithTheLayout_AndReset()
    {
        Layout.Move("browser", ToolWindowLocation.Editor);
        Layout.Save();

        using var restored = new LayoutFixture(_store, _fixture.Registry);
        restored.Layout.Load();

        Assert.Equal(ToolWindowLocation.Editor, restored.Placement.Find("browser")!.Location);
        restored.Placement.Reset();
        Assert.Equal("browser", Assert.Single(restored.Layout.Panel.Items).Id);
    }

    // Keyboard path: the palette picks the tool window, then the area.
    [Fact]
    public async Task MoveViewCommand_PicksTheViewThenTheArea()
    {
        var quickPick = new FakeQuickPick();
        using var commands = new LayoutCommands(Layout, _fixture.EditorWindows, quickPick);
        commands.Register(_fixture.Shell.Commands, _fixture.Shell.Keybindings, _fixture.Shell.Menus, _fixture.Registry);

        await _fixture.Shell.CommandService.ExecuteAsync(LayoutCommands.MoveViewId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(("Браузер", "Нижняя панель"), (quickPick.Items[0].Title, quickPick.Items[0].Detail));
        await quickPick.PickAsync("Браузер");
        await quickPick.PickAsync("Область редактора");

        Assert.Equal(EditorToolWindows.KeyOf("browser"), Assert.Single(_fixture.Editors.Area.Tabs).Key);
    }
}
