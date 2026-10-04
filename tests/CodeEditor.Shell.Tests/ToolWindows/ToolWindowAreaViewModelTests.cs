using CodeEditor.Shell.Tests.Infrastructure;
using CodeEditor.Shell.ToolWindows;

namespace CodeEditor.Shell.Tests.ToolWindows;

public sealed class ToolWindowAreaViewModelTests : IDisposable
{
    private const double DefaultSize = 200;
    private const double MinSize = 100;

    private readonly ShellFixture _shell = new();
    private readonly ToolWindowRegistry _registry = new();
    private readonly ToolWindowAreaViewModel _area;
    private int _created;

    public ToolWindowAreaViewModelTests()
    {
        _area = new ToolWindowAreaViewModel(ToolWindowLocation.Panel, DefaultSize, MinSize, new ToolWindowPlacement(_registry, _shell.Keybindings));
        Register("output", order: 2);
        Register("problems", order: 1);
    }

    public void Dispose() => _area.Dispose();

    [Fact]
    public void Items_FollowRegistryOrder_AndAreaStartsHidden()
    {
        Assert.Equal(["problems", "output"], _area.Items.Select(item => item.Id));
        Assert.False(_area.IsVisible);
        Assert.Equal(0, _area.GridSize);
    }

    [Fact]
    public void Show_ActivatesAndCreatesContentLazily()
    {
        Assert.Equal(0, _created);

        Assert.True(_area.Show("output"));
        _ = _area.Active!.Content;

        Assert.True(_area.IsVisible);
        Assert.Equal("output", _area.Active.Id);
        Assert.True(_area.Active.IsActive);
        Assert.Equal(1, _created);
        Assert.Equal(DefaultSize, _area.GridSize);
    }

    [Fact]
    public void ToggleItem_OnActiveVisible_Hides_OtherwiseShows()
    {
        var output = _area.Items.Single(item => item.Id == "output");

        _area.ToggleItem(output);
        Assert.True(_area.IsVisible);

        _area.ToggleItem(output);
        Assert.False(_area.IsVisible);
    }

    [Fact]
    public void Toggle_ShowsLastActiveOrFirst()
    {
        _area.Toggle();
        Assert.Equal("problems", _area.Active?.Id);

        _area.Show("output");
        _area.Toggle();
        _area.Toggle();

        Assert.True(_area.IsVisible);
        Assert.Equal("output", _area.Active?.Id);
    }

    [Fact]
    public void GridSize_ClampsToMinimum_AndIsIgnoredWhileHidden()
    {
        _area.Show("output");

        _area.GridSize = 10;
        Assert.Equal(MinSize, _area.Size);

        _area.Hide();
        _area.GridSize = 500;
        Assert.Equal(MinSize, _area.Size);
    }

    [Fact]
    public void Restore_WaitsForToolWindowRegisteredLater()
    {
        _area.Restore(new ToolWindowAreaState(IsVisible: true, Size: 300, ActiveId: "terminal"));
        Assert.Null(_area.Active);

        Register("terminal", order: 3);

        Assert.Equal("terminal", _area.Active?.Id);
        Assert.Equal(300, _area.Size);
    }

    [Fact]
    public void Capture_RoundTripsState()
    {
        _area.Show("output");
        _area.GridSize = 321;

        Assert.Equal(new ToolWindowAreaState(true, 321, "output"), _area.Capture());
    }

    [Fact]
    public void UnregisteringActive_HidesArea()
    {
        var registration = Register("terminal", order: 3);
        _area.Show("terminal");

        registration.Dispose();

        Assert.False(_area.IsVisible);
        Assert.Null(_area.Active);
    }

    [Fact]
    public void Rebuild_KeepsCreatedContent()
    {
        _area.Show("output");
        var content = _area.Active!.Content;

        Register("terminal", order: 3);

        Assert.Same(content, _area.Items.Single(item => item.Id == "output").Content);
    }

    [Fact]
    public void ToolTip_IncludesShowCommandShortcut()
    {
        _shell.Bind("Ctrl+Shift+U", ToolWindowAreaViewModel.ShowCommandId("output"));

        Assert.Equal("output (Ctrl+Shift+U)", _area.Items.Single(item => item.Id == "output").ToolTip);
    }

    [Fact]
    public void Registry_RejectsDuplicateId()
    {
        Assert.Throws<InvalidOperationException>(() => Register("output", order: 5));
    }

    private IDisposable Register(string id, int order) =>
        _registry.Register(new ToolWindowDefinition(id, id, "", ToolWindowLocation.Panel, () =>
        {
            _created++;
            return new object();
        })
        {
            Order = order,
        });
}
