using System.Collections.ObjectModel;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// The current chat: agent turns (<see cref="AgentTurn"/>), stop, errors, saving to the folder history after each
/// answer, background memory updates after a turn, and opening, starting and deleting chats.
/// </summary>
public sealed partial class ChatSession : ObservableObject, IDisposable
{
    public static string PlanReadyNotice => TurnEnding.PlanReadyNotice;

    private readonly AgentTurnServices _services;
    private readonly ChatHistory _history;
    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly TurnMessageBuilder _turnMessages;
    private readonly HashSet<string> _approvedTools = new(StringComparer.Ordinal);
    private readonly TurnResume _resume;
    private readonly TurnMemory _memory;
    private CancellationTokenSource? _run;
    private Task _turn = Task.CompletedTask;

    // The folder changed under a running turn: its chat belongs to the previous folder and can no longer be saved.
    private bool _orphaned;

    // The previous turn's mode in this chat, so the model is told about mode changes (TurnMessageBuilder).
    private AgentMode? _lastMode;

    public ChatSession(AgentTurnServices services, ChatHistory history, IOptionsMonitor<AgentOptions> options, TurnMessageBuilder turnMessages, IUiDispatcher dispatcher)
    {
        _services = services;
        _history = history;
        _options = options;
        _turnMessages = turnMessages;
        _resume = new TurnResume(Messages);

        // Every row gets the time it appeared: saved with the chat, it shows afterwards how long each step took.
        Messages.CollectionChanged += (_, e) =>
        {
            foreach (var message in e.NewItems?.OfType<ChatMessageViewModel>() ?? [])
            {
                message.At ??= _services.Time.GetLocalNow();
            }
        };
        _memory = new TurnMemory(services, history, Messages, dispatcher);
        _services.Questions.Asked += OnQuestionAsked;
    }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    public string Title => _history.CurrentTitle;

    public string? CurrentId => _history.CurrentId;

    /// <summary>Raised after a stop or error with the text of queued messages that never reached the model.</summary>
    public event EventHandler<string>? QueueReturned;

    /// <summary>
    /// Sends a message and runs the agent turn to the end, including approval decisions. While the agent works, the
    /// message is queued (ADR 0026) and goes to the model at the next step boundary.
    /// </summary>
    /// <param name="includeActiveEditor">Tell the agent the open file and selection (the file chip is on).</param>
    /// <param name="attachment">The file the agent will see, shown as a line under the message.</param>
    /// <param name="files">Attached files: text goes in as content, images as pictures.</param>
    public async Task SendAsync(string text, bool includeActiveEditor, string? attachment, IReadOnlyList<string>? files = null)
    {
        files ??= [];
        IReadOnlyList<string> names = [.. files.Select(Path.GetFileName).OfType<string>()];
        if (IsBusy)
        {
            var message = await BuildAsync(text, includeActiveEditor, startsChat: false, files, CancellationToken.None);
            _services.Queue.Enqueue(new QueuedMessage(text, message, names, attachment));
            return;
        }

        _resume.Retire();
        var startsChat = !Messages.Any(message => message.Kind == ChatMessageKind.User);
        var options = _options.CurrentValue;
        Messages.Add(new ChatMessageViewModel(ChatMessageKind.User, text) { Attachment = attachment, Files = names.Count > 0 ? names : null, Model = options.Model, Mode = options.Mode });
        _turn = RunTurnAsync(token => BuildAsync(text, includeActiveEditor, startsChat, files, token), text.Length, attachment, files.Count);
        await _turn;
    }

    private async Task RunTurnAsync(Func<CancellationToken, Task<ChatMessage>> message, int length, string? attachment, int files)
    {
        _run?.Dispose();
        _run = new CancellationTokenSource();
        var options = _options.CurrentValue;
        var turn = new AgentTurn(Messages, _services, _approvedTools, options.Approvals, AgentModes.RequestLimitOf(options));
        var started = _services.Log.TurnStarted(CurrentId, length, attachment, files);
        IsBusy = true;
        var (completed, stopped) = (false, false);
        try
        {
            var mode = _options.CurrentValue.Mode;
            await turn.RunAsync(await message(_run.Token), mode, _run.Token);
            _services.Log.TurnFinished(started, turn.ToolCalls, turn.Approvals);
            TurnEnding.AddNotice(Messages, turn, mode);
            completed = true;
        }
        catch (OperationCanceledException) when (_run.IsCancellationRequested)
        {
            _services.Log.TurnStopped(started);
            turn.RejectPendingApprovals();
            stopped = true;
        }
        catch (Exception exception)
        {
            // Includes a cancellation nobody asked for: an HTTP timeout.
            _services.Log.TurnFailed(started, exception);
            _resume.AfterError(AgentErrors.Describe(exception, _options.CurrentValue), AgentErrors.CanContinue(exception));
        }
        finally
        {
            IsBusy = false;
            if (!_orphaned)
            {
                TurnEnding.EstimateUsage(Messages, turn, _services.Usage);
                await _history.SaveAsync([.. Messages], _services.Usage.Usage, CancellationToken.None);
                OnPropertyChanged(nameof(Title));
            }
        }

        // After saving: a new chat gets its id on the first save, and the stop notice stays out of the history.
        if (completed)
        {
            Remembering = _memory.RememberAsync(turn, _run.Token);
        }
        else if (stopped)
        {
            _resume.AfterStop();
        }

        await ContinueWithQueueAsync(completed);
    }

    // Queued messages run as the next turn after a completed one; after a stop or error they go back to the input box.
    private async Task ContinueWithQueueAsync(bool completed)
    {
        if (!completed)
        {
            if (_services.Queue.TakeAll() is { Count: > 0 } returned)
            {
                QueueReturned?.Invoke(this, string.Join(Environment.NewLine + Environment.NewLine, returned.Select(queued => queued.Text)));
            }

            return;
        }

        if (_services.Queue.TryDequeue(out var next))
        {
            Messages.Add(ChatMessageViewModel.FromUser(next));
            await RunTurnAsync(_ => Task.FromResult(next.Message), next.Text.Length, next.Attachment, next.Files.Count);
        }
    }

    // Passes the previous message's mode so a mode change is marked (TurnMessageBuilder).
    private Task<ChatMessage> BuildAsync(string text, bool includeActiveEditor, bool startsChat, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var mode = _options.CurrentValue.Mode;
        var previousMode = startsChat ? null : _lastMode;
        _lastMode = mode;
        return _turnMessages.BuildAsync(text, includeActiveEditor, startsChat, previousMode, files, cancellationToken);
    }

    public void Stop() => _run?.Cancel();

    /// <summary>The memory update after the last turn; tests await it.</summary>
    public Task Remembering { get; private set; } = Task.CompletedTask;

    /// <summary>Opens the latest chat of the folder (on startup and folder change).</summary>
    public async Task LoadLatestAsync()
    {
        _orphaned = IsBusy;
        await StopTurnAsync();
        _orphaned = false;
        Show(_history.RestoreLatest());
    }

    /// <returns><c>false</c> if the chat no longer exists (its file was deleted).</returns>
    public async Task<bool> OpenAsync(string id)
    {
        await StopTurnAsync();
        if (_history.Restore(id) is not { } transcript)
        {
            return false;
        }

        _services.Log.ChatOpened(id);
        Show(transcript);
        return true;
    }

    public async Task StartNewAsync()
    {
        await StopTurnAsync();
        _history.StartNew();
        _services.Log.ChatStarted();
        Show(null);
    }

    public async Task DeleteCurrentAsync()
    {
        await StopTurnAsync();
        _services.Log.ChatDeleted(CurrentId);
        _history.DeleteCurrent();
        Show(null);
    }

    // Another chat opens only after the running turn has wound down: the stopped turn is saved into its own chat,
    // and nothing of it (the stop mark, an error) lands in the feed and the file of the next one.
    private async Task StopTurnAsync()
    {
        Stop();
        await _turn.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
    }
    public void Dispose()
    {
        _services.Questions.Asked -= OnQuestionAsked;
        _run?.Cancel();
        _run?.Dispose();
    }

    private void Show(ChatTranscript? transcript)
    {
        // Another chat means another model history: files read in the previous chat are unseen, its turn mode is
        // unknown, and queued messages belonged to the previous chat.
        _services.Queue.TakeAll();
        _approvedTools.Clear();
        _lastMode = null;
        _services.FileState.Reset();
        _services.ApprovalCards.ResetChat();
        _services.Todos.Clear();
        Messages.Clear();
        foreach (var message in transcript?.Messages ?? [])
        {
            // An approval card is saved as its decision text; a restored feed shows it as a tool row.
            var kind = message.Kind == ChatMessageKind.Approval ? ChatMessageKind.Tool : message.Kind;
            Messages.Add(new ChatMessageViewModel(kind, message.Text)
            {
                Attachment = message.Attachment,
                Files = message.Files,
                At = message.At,
                Model = message.Model,
                Mode = message.Mode,
                Tool = message.Tool is { } tool ? new ToolRowViewModel(tool.ToView(message.Text)) : null,
            });
        }

        _services.Usage.Restore(transcript?.Usage);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CurrentId));
    }

    // An agent question is a card in the feed; the answer is appended to the message text to stay in the chat history.
    private void OnQuestionAsked(object? sender, QuestionCardViewModel card)
    {
        var message = new ChatMessageViewModel(ChatMessageKind.Question, card.Question) { Question = card };
        Messages.Add(message);
        _ = RecordAnswerAsync(message, card);
    }

    private static async Task RecordAnswerAsync(ChatMessageViewModel message, QuestionCardViewModel card)
    {
        try
        {
            message.Append("\n→ " + await card.Answer);
        }
        catch (OperationCanceledException)
        {
            message.Append("\n" + Strings.QuestionWithdrawnNote);
        }
    }
}
