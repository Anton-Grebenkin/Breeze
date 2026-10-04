using System.Collections.ObjectModel;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.ViewModels.Activity;
using CodeEditor.Modules.Agent.ViewModels.Approvals;
using CodeEditor.Modules.Agent.ViewModels.Chat;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>Turn feed (ADR 0010): activity blocks from a flat message list, exploration groups, collapsing.</summary>
public sealed class ChatFeedTests : IDisposable
{
    private readonly ObservableCollection<ChatMessageViewModel> _messages = [];
    private readonly ManualTime _time = new();
    private readonly ChatFeed _feed;
    private bool _busy;

    public ChatFeedTests() => _feed = new ChatFeed(_messages, () => _busy, _time);

    public void Dispose() => _feed.Dispose();

    [Fact]
    public void ActivityRun_BecomesOneBlock_BetweenMessages()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _messages.Add(Message(ChatMessageKind.Reasoning));
        _messages.Add(Message(ChatMessageKind.Progress));
        _messages.Add(Tool(AgentToolIcon.Edit));
        _messages.Add(Message(ChatMessageKind.Assistant));

        Assert.Equal(3, _feed.Items.Count);
        var block = Assert.IsType<ActivityBlockViewModel>(_feed.Items[1]);
        Assert.Collection(block.Items,
            item => Assert.IsType<ThinkingItemViewModel>(item),
            item => Assert.Equal(ChatMessageKind.Progress, Assert.IsType<ActivityEntryViewModel>(item).Message.Kind),
            item => Assert.Equal(ChatMessageKind.Tool, Assert.IsType<ActivityEntryViewModel>(item).Message.Kind));
        Assert.True(block.Items[0].IsFirst);
        Assert.True(block.Items[^1].IsLast);
        Assert.False(block.Items[1].IsFirst || block.Items[1].IsLast);
    }

    [Fact]
    public void ConsecutiveExploration_CollapsesIntoGroup_WithSummary()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _messages.Add(Tool(AgentToolIcon.Read, exploration: true));
        _messages.Add(Tool(AgentToolIcon.Read, exploration: true));
        _messages.Add(Tool(AgentToolIcon.Search, exploration: true));
        _messages.Add(Tool(AgentToolIcon.Edit));
        _messages.Add(Tool(AgentToolIcon.Read, exploration: true));

        var block = Assert.IsType<ActivityBlockViewModel>(_feed.Items[1]);
        var group = Assert.IsType<ExplorationGroupViewModel>(block.Items[0]);
        Assert.Equal("Изучено: 2 файла, 1 поиск", group.Title);
        Assert.Equal(3, block.Items.Count);
        Assert.IsType<ActivityEntryViewModel>(block.Items[2]);
    }

    [Fact]
    public void LiveTurn_InsertsBeforeAnswer_KeepsOneBlockAcrossRounds_AndCollapsesWhenDone()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _busy = true;
        _feed.OnBusyChanged();

        // Like AgentTurn: steps go before a placeholder answer, which is dropped when empty and re-added each round.
        var first = Message(ChatMessageKind.Assistant);
        _messages.Add(first);
        _messages.Insert(1, Tool(AgentToolIcon.Edit));
        _time.Advance(TimeSpan.FromSeconds(3));
        _messages.Remove(first);
        _messages.Add(Message(ChatMessageKind.Assistant));
        _messages.Insert(2, Tool(AgentToolIcon.Build));
        _time.Advance(TimeSpan.FromSeconds(4));
        _messages[^1].Text = "Готово.";

        var block = Assert.IsType<ActivityBlockViewModel>(Assert.Single(_feed.Items.OfType<ActivityBlockViewModel>()));
        Assert.Equal(2, block.ToolCount);
        Assert.True(block.IsRunning && block.IsExpanded);
        Assert.Equal("Работает · 7 с", block.Title);
        Assert.Same(block, _feed.Items[1]);
        var answer = Assert.IsType<ChatMessageViewModel>(_feed.Items[2]);

        // While the turn runs, the answer is the chain's tail: the line from the last step continues to it.
        Assert.True(answer.IsChainTail && block.HasTail);
        Assert.False(block.Items[^1].IsLast);

        _busy = false;
        _feed.OnBusyChanged();

        Assert.False(block.IsRunning || block.IsExpanded);
        Assert.Equal("Работал 3 с · 2 действия", block.Title);
        Assert.False(answer.IsChainTail || block.HasTail);
        Assert.True(block.Items[^1].IsLast);
    }

    [Fact]
    public void RunningStep_HidesWaitingInTail_UntilItEnds()
    {
        _busy = true;
        _messages.Add(Message(ChatMessageKind.User));
        var answer = new ChatMessageViewModel(ChatMessageKind.Assistant) { IsInProgress = true };
        _messages.Add(answer);
        var build = Tool(AgentToolIcon.Build);
        build.IsInProgress = true;
        _messages.Insert(1, build);

        Assert.True(answer.IsChainTail && answer.IsChainBusy);
        var block = Assert.IsType<ActivityBlockViewModel>(_feed.Items[1]);
        Assert.True(block.Items[^1].IsLast);

        build.IsInProgress = false;

        Assert.False(answer.IsChainBusy);
        Assert.False(block.Items[^1].IsLast);
    }

    [Fact]
    public void PendingApproval_NoWaitingUnderIt_UntilDecided()
    {
        _busy = true;
        _messages.Add(Message(ChatMessageKind.User));
        var card = new ApprovalCardViewModel(new Microsoft.Extensions.AI.ToolApprovalRequestContent("r1", new Microsoft.Extensions.AI.FunctionCallContent("c1", "run_command")), "Команда", []);
        _messages.Add(new ChatMessageViewModel(ChatMessageKind.Approval) { Approval = card });
        var answer = new ChatMessageViewModel(ChatMessageKind.Assistant) { IsInProgress = true };
        _messages.Add(answer);

        Assert.True(answer.IsChainBusy);
        Assert.False(answer.IsChainTail);

        card.ApproveCommand.Execute(null);

        Assert.False(answer.IsChainBusy);
    }

    [Fact]
    public void Answer_WithoutRunningBlock_IsOrdinaryMessage()
    {
        _busy = true;
        _messages.Add(Message(ChatMessageKind.User));
        var answer = Message(ChatMessageKind.Assistant);
        _messages.Add(answer);

        Assert.False(answer.IsChainTail);
    }

    [Fact]
    public void ApprovalCard_SplitsTheChain()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _messages.Add(Tool(AgentToolIcon.Read, exploration: true));
        _messages.Add(Message(ChatMessageKind.Approval));
        _messages.Add(Tool(AgentToolIcon.Build));

        Assert.Equal(2, _feed.Items.OfType<ActivityBlockViewModel>().Count());
    }

    [Fact]
    public void RestoredChat_BlocksAreCollapsed_WithoutTiming()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _messages.Add(Tool(AgentToolIcon.Read, exploration: true));
        _messages.Add(Tool(AgentToolIcon.Edit));

        var block = Assert.IsType<ActivityBlockViewModel>(_feed.Items[1]);
        Assert.False(block.IsExpanded);
        Assert.Equal("Ход работы · 2 действия", block.Title);

        block.ToggleCommand.Execute(null);

        Assert.True(block.IsExpanded);
    }

    [Fact]
    public void Reset_RebuildsFromCurrentMessages()
    {
        _messages.Add(Message(ChatMessageKind.User));
        _messages.Add(Tool(AgentToolIcon.Edit));

        _messages.Clear();
        _messages.Add(Message(ChatMessageKind.User));

        Assert.Single(_feed.Items);
    }

    [Fact]
    public void LiveThinking_ShownExpanded_ThenTitledWithDuration()
    {
        _busy = true;
        var thought = new ChatMessageViewModel(ChatMessageKind.Reasoning) { IsInProgress = true };
        _messages.Add(thought);
        var thinking = Assert.IsType<ThinkingItemViewModel>(Assert.IsType<ActivityBlockViewModel>(_feed.Items[0]).Items[0]);

        thought.Append("строка 1\nстрока 2");
        Assert.Equal("Размышляет…", thinking.Title);
        _time.Advance(TimeSpan.FromSeconds(6));
        // Long reasoning shows its duration; without it the feed looks frozen.
        var changed = new List<string?>();
        thinking.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _feed.Tick();
        Assert.Contains(nameof(ThinkingItemViewModel.Title), changed);
        Assert.Equal("Размышляет · 6 с", thinking.Title);
        Assert.True(thinking.IsExpanded);

        thought.IsInProgress = false;

        Assert.Equal("Размышлял 6 с", thinking.Title);
        Assert.True(thinking.IsExpanded);
    }

    [Fact]
    public void RestoredThinking_Collapsed()
    {
        _messages.Add(new ChatMessageViewModel(ChatMessageKind.Reasoning, "старые рассуждения"));

        var thinking = Assert.IsType<ThinkingItemViewModel>(Assert.IsType<ActivityBlockViewModel>(_feed.Items[0]).Items[0]);

        Assert.False(thinking.IsExpanded);
    }

    [Theory]
    [InlineData(0.2, "1 с")]
    [InlineData(42, "42 с")]
    [InlineData(65, "1 мин 5 с")]
    [InlineData(720, "12 мин")]
    public void Duration_IsShort(double seconds, string text) => Assert.Equal(text, DurationText.Format(TimeSpan.FromSeconds(seconds)));

    private static ChatMessageViewModel Message(ChatMessageKind kind) => new(kind, kind.ToString());

    private static ChatMessageViewModel Tool(AgentToolIcon icon, bool exploration = false) =>
        new(ChatMessageKind.Tool, icon.ToString()) { Tool = new ToolRowViewModel(new AgentToolView(icon, icon.ToString()) { IsExploration = exploration }) };

    /// <summary>Timestamp clock the test advances by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
}
