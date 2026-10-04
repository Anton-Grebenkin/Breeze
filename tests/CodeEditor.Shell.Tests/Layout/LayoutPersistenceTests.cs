using CodeEditor.Core.Storage;
using CodeEditor.Shell.Layout;
using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Layout;

public sealed class LayoutPersistenceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", Guid.NewGuid().ToString("N"));
    private readonly JsonLayoutStore _store;

    public LayoutPersistenceTests() =>
        _store = new JsonLayoutStore(new UserDataPaths(_folder), NullLogger<JsonLayoutStore>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutFile_ReturnsNull()
    {
        Assert.Null(_store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var state = new LayoutState(
            new ToolWindowAreaState(true, 280, "explorer"),
            new ToolWindowAreaState(false, 190, "output"),
            new WindowPlacement(10, 20, 1300, 900, IsMaximized: true));

        _store.Save(state);

        Assert.Equal(state, _store.Load());
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsNull()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "layout.json"), "{ не json");

        Assert.Null(_store.Load());
    }

    [Fact]
    public void WorkbenchLayout_SavesAndRestoresAreas()
    {
        var shell = new ShellFixture();
        var registry = new ToolWindowRegistry();
        registry.Register(new ToolWindowDefinition("output", "Вывод", "", ToolWindowLocation.Panel, () => new object()));

        using (var firstFixture = new LayoutFixture(_store, registry))
        {
            var first = firstFixture.Layout;
            first.Panel.Show("output");
            first.Panel.GridSize = 333;
            first.Window = new WindowPlacement(1, 2, 800, 600, IsMaximized: false);
            first.Save();
        }

        using var secondFixture = new LayoutFixture(_store, registry);
        var second = secondFixture.Layout;
        second.Load();

        Assert.True(second.Panel.IsVisible);
        Assert.Equal("output", second.Panel.Active?.Id);
        Assert.Equal(333, second.Panel.Size);
        Assert.False(second.SideBar.IsVisible);
        Assert.Equal(new WindowPlacement(1, 2, 800, 600, false), second.Window);
    }
}
