using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Turn;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.ViewModels.Activity;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>A slow tool row: elapsed time, the last output line and "To background" while the call runs.</summary>
public sealed class LiveToolRowsTests : IDisposable
{
    private readonly ManualTimeProvider _time = new();
    private readonly RunningToolCalls _calls;
    private readonly Dictionary<string, ToolRowViewModel> _rows = [];
    private readonly LiveToolRows _live;

    public LiveToolRowsTests()
    {
        _calls = new RunningToolCalls(_time);
        _live = new LiveToolRows(_calls, id => _rows.GetValueOrDefault(id), _time, new InlineUiDispatcher());
    }

    public void Dispose() => _live.Dispose();

    [Fact]
    public void SlowCall_ShowsProgressAndButton_ThenClearsWhenItEnds()
    {
        var row = _rows["c1"] = new ToolRowViewModel(new AgentToolView(AgentToolIcon.Test, "Тесты"));
        var registration = _calls.Begin("c1");
        var call = (IAgentToolCall)registration;
        var release = call.OfferBackground();
        call.Report("Passed OrderTests.Total");

        _time.Advance(TimeSpan.FromSeconds(1));
        _live.Refresh();
        Assert.False(row.HasProgress);

        _time.Advance(TimeSpan.FromSeconds(4));
        _live.Refresh();
        Assert.Equal((DurationText.Format(TimeSpan.FromSeconds(5)), "Passed OrderTests.Total", true), (row.Elapsed, row.Output, row.CanMoveToBackground));

        row.MoveToBackgroundCommand.Execute(null);
        _live.Refresh();
        Assert.True(release.IsCancellationRequested);
        Assert.False(row.CanMoveToBackground);

        registration.Dispose();
        _live.Refresh();
        Assert.False(row.HasProgress);
        Assert.Null(row.Output);
    }

    [Fact]
    public void ToolWithoutProcess_HasNoButton()
    {
        var row = _rows["c1"] = new ToolRowViewModel(new AgentToolView(AgentToolIcon.Search, "Поиск"));
        using var registration = _calls.Begin("c1");

        _time.Advance(TimeSpan.FromSeconds(3));
        _live.Refresh();

        Assert.True(row.HasProgress);
        Assert.False(row.CanMoveToBackground);
    }
}
