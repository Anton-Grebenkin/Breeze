using System.ComponentModel;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// Tracks the live turn's answer and what is above it (ADR 0010). Under a running work block the answer is the chain
/// tail: it prints as the next step, so switching to tools moves nothing. "Thinking…" shows only while the model is
/// really preparing the answer, not while a tool runs, reasoning streams or a card awaits the user.
/// </summary>
public sealed class ChainTail : IDisposable
{
    private ChatMessageViewModel? _answer;
    private ActivityBlockViewModel? _block;
    private INotifyPropertyChanged? _card;
    private Func<bool>? _cardPending;

    /// <param name="answer">The live turn's answer if it is last in the feed; otherwise <c>null</c>.</param>
    /// <param name="above">The feed item above the answer: a work block, a card or a message.</param>
    public void Update(ChatMessageViewModel? answer, object? above)
    {
        var block = answer is not null && above is ActivityBlockViewModel { IsRunning: true } running ? running : null;
        var (card, pending) = answer is null ? (null, null) : WaitingCard(above);
        if (ReferenceEquals(answer, _answer) && ReferenceEquals(block, _block) && ReferenceEquals(card, _card))
        {
            return;
        }

        Detach();
        (_answer, _block, _card, _cardPending) = (answer, block, card, pending);
        Attach();
        Refresh();
    }

    public void Dispose() => Detach();

    // A card that can wait for the user: an approval or an agent question.
    private static (INotifyPropertyChanged? Card, Func<bool>? Pending) WaitingCard(object? above) => above switch
    {
        ChatMessageViewModel { Approval: { } approval } => (approval, () => approval.IsPending),
        ChatMessageViewModel { Question: { } question } => (question, () => question.IsPending),
        _ => (null, null),
    };

    private void Attach()
    {
        if (_answer is not null)
        {
            _answer.PropertyChanged += OnChanged;
        }

        if (_block is not null)
        {
            _block.PropertyChanged += OnChanged;
        }

        if (_card is not null)
        {
            _card.PropertyChanged += OnChanged;
        }
    }

    private void Detach()
    {
        if (_answer is not null)
        {
            _answer.PropertyChanged -= OnChanged;
            _answer.IsChainTail = false;
            _answer.IsChainBusy = false;
        }

        if (_block is not null)
        {
            _block.PropertyChanged -= OnChanged;
            _block.HasTail = false;
        }

        if (_card is not null)
        {
            _card.PropertyChanged -= OnChanged;
        }

        (_answer, _block, _card, _cardPending) = (null, null, null, null);
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ActivityBlockViewModel.HasRunningStep) or nameof(ChatMessageViewModel.Text) or nameof(ApprovalCardViewModel.IsPending))
        {
            Refresh();
        }
    }

    // The chain line reaches the tail only when it is visible; an empty tail is hidden while a step runs.
    private void Refresh()
    {
        if (_answer is null)
        {
            return;
        }

        _answer.IsChainTail = _block is not null;
        _answer.IsChainBusy = _block?.HasRunningStep ?? _cardPending?.Invoke() ?? false;
        if (_block is not null)
        {
            _block.HasTail = !_answer.IsChainBusy || _answer.Text.Length > 0;
        }
    }
}
