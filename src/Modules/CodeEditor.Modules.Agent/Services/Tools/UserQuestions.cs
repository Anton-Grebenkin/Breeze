using CodeEditor.Core.Threading;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Bridge from the <c>ask_user</c> tool (background thread) to the chat feed: the question card is created and shown on
/// the UI thread while the tool waits for the answer. Cancelling the turn withdraws the question.
/// </summary>
public sealed class UserQuestions(IUiDispatcher dispatcher)
{
    /// <summary>A question appeared; the feed adds a card (on the UI thread).</summary>
    public event EventHandler<QuestionCardViewModel>? Asked;

    public async Task<string> AskAsync(string question, IReadOnlyList<string> options, CancellationToken cancellationToken)
    {
        var card = new QuestionCardViewModel(question, options);
        await dispatcher.InvokeAsync(() => Asked?.Invoke(this, card));
        await using var registration = cancellationToken.Register(() => dispatcher.Post(card.Cancel));
        return await card.Answer.WaitAsync(cancellationToken);
    }
}
