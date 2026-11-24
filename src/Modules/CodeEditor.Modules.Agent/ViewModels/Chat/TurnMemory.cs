using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// The memory update after a turn (ADR 0012): a helper model reads the turn and decides what to remember. Runs in the
/// background with the input unlocked; what it saved shows as rows under the turn and is saved with the chat.
/// </summary>
internal sealed class TurnMemory(AgentTurnServices services, ChatHistory history, ObservableCollection<ChatMessageViewModel> messages, IUiDispatcher dispatcher)
{
    private readonly HashSet<(string? Chat, string Note)> _shown = [];

    public async Task RememberAsync(AgentTurn turn, CancellationToken cancellationToken)
    {
        var chatId = history.CurrentId;
        if (!services.MemoryExtractor.ShouldRun(turn.ToolCalls, messages.Count(message => message.Kind == ChatMessageKind.User)))
        {
            return;
        }

        var answer = messages.LastOrDefault(message => message.Kind == ChatMessageKind.Assistant)?.Text ?? string.Empty;
        var notes = await services.MemoryExtractor.ExtractAsync(answer, cancellationToken).ConfigureAwait(false);
        if (notes.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        ChatMessageViewModel[]? snapshot = null;
        await dispatcher.InvokeAsync(() =>
        {
            // The user may have opened another chat; the "Remembered" row belongs to the turn that produced the notes.
            if (history.CurrentId != chatId)
            {
                return;
            }

            // One "Remembered" row per note in a chat: a note extended on a later turn does not repeat it.
            foreach (var note in notes.Where(note => _shown.Add((chatId, note.Name))))
            {
                messages.Add(new ChatMessageViewModel(ChatMessageKind.Status, string.Format(CultureInfo.CurrentCulture, Strings.MemoryAutoSaved, note.Name, note.Description)));
            }

            snapshot = [.. messages];
        }).ConfigureAwait(false);
        if (snapshot is not null)
        {
            await history.SaveAsync(snapshot, services.Usage.Usage, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
