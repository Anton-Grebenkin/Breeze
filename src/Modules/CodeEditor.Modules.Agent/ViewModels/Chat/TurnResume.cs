using System.Collections.ObjectModel;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// "Continue" after a turn that was stopped or failed: a button under the feed's last row. It is offered only until the
/// chat moves on, so old rows keep no buttons; the stop notice is added after saving and is not part of the history.
/// </summary>
internal sealed class TurnResume(ObservableCollection<ChatMessageViewModel> messages)
{
    private ChatMessageViewModel? _offer;

    public void AfterStop() => Offer(new ChatMessageViewModel(ChatMessageKind.Notice, Strings.TurnStoppedNotice));

    /// <param name="canContinue">Repeating may help: the error is not one the user has to fix first.</param>
    public void AfterError(string text, bool canContinue) =>
        Offer(new ChatMessageViewModel(ChatMessageKind.Error, text) { CanContinue = canContinue });

    /// <summary>The chat moves on: the stop notice goes away, the error stays without its button.</summary>
    public void Retire()
    {
        if (_offer is { Kind: ChatMessageKind.Notice } notice)
        {
            messages.Remove(notice);
        }
        else if (_offer is not null)
        {
            _offer.CanContinue = false;
        }

        _offer = null;
    }

    private void Offer(ChatMessageViewModel message)
    {
        Retire();
        _offer = message;
        messages.Add(message);
    }
}
