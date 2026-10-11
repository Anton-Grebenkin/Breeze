using CodeEditor.Modules.Agent.Contracts.Turn;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Tool calls in progress: the last output line, elapsed time and "To background" for the feed.</summary>
public sealed class RunningToolCallsTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly RunningToolCalls _calls;

    public RunningToolCallsTests() => _calls = new RunningToolCalls(_time);

    [Fact]
    public void OutsideACall_CurrentIsIdle()
    {
        var call = _calls.Current();
        call.Report("line");

        Assert.False(call.OfferBackground().CanBeCanceled);
        Assert.Empty(_calls.Snapshot());
    }

    [Fact]
    public void Call_KeepsLastNonBlankLine_AndGoesToBackgroundOnlyWhenOffered()
    {
        using var registration = _calls.Begin("c1");
        var call = (IAgentToolCall)registration;
        call.Report("Building…");
        call.Report("   ");
        _time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(new ToolCallState("c1", TimeSpan.FromSeconds(3), "Building…", OffersBackground: false), Assert.Single(_calls.Snapshot()));
        Assert.False(_calls.MoveToBackground("c1"));

        var release = call.OfferBackground();
        Assert.True(Assert.Single(_calls.Snapshot()).OffersBackground);
        Assert.True(_calls.MoveToBackground("c1"));
        Assert.True(release.IsCancellationRequested);
        Assert.False(Assert.Single(_calls.Snapshot()).OffersBackground);
    }

    [Fact]
    public void EndedCall_LeavesTheSnapshot()
    {
        _calls.Begin("c1").Dispose();

        Assert.Empty(_calls.Snapshot());
        Assert.False(_calls.MoveToBackground("c1"));
    }

    [Fact]
    public void MoveAll_MovesOnlyCallsWaitingForAProcess()
    {
        using var waiting = _calls.Begin("a");
        using var reading = _calls.Begin("b");
        ((IAgentToolCall)waiting).OfferBackground();

        Assert.Equal(1, _calls.MoveAllToBackground());
        Assert.Equal(0, _calls.MoveAllToBackground());
    }
}
