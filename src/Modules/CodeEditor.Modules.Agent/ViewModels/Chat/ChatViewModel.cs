using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Commands;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.Palette;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ToolWindows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>
/// The Agent panel (<c>Ctrl+Alt+I</c>): Markdown feed, input box, model and parameters, context fill and the folder's
/// chat history. Every action is available both as a panel button and as a palette command.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject, IFocusableContent, IDisposable
{
    public const int RecentChatsShown = 5;

    public static string ContinueText => Strings.ContinueRequest;

    public static string ExecutePlanText => Strings.ExecutePlanRequest;

    private readonly ChatHistory _history;
    private readonly ChatLinkOpener _links;
    private readonly IQuickPick _quickPick;
    private readonly ISystemShell _shell;
    private readonly IWorkspace _workspace;
    private readonly ApiKeyState _apiKey;
    private readonly ICommandService _commands;
    private readonly TimeProvider _time;

    public ChatViewModel(
        ChatSession session,
        ChatHistory history,
        ChatPanelParts parts,
        ChatLinkOpener links,
        IQuickPick quickPick,
        ISystemShell shell,
        IWorkspace workspace,
        ApiKeyState apiKey,
        ICommandService commands,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Session = session;
        (Mode, Model, Context, ActiveFile, Todo, Changes, Attachments, Queued) = (parts.Mode, parts.Model, parts.Context, parts.ActiveFile, parts.Todo, parts.Changes, parts.Attachments, parts.Queued);
        _history = history;
        _links = links;
        _quickPick = quickPick;
        _shell = shell;
        _workspace = workspace;
        _apiKey = apiKey;
        _commands = commands;
        _time = time;
        Feed = new ChatFeed(session.Messages, () => session.IsBusy, time);

        Session.PropertyChanged += OnSessionPropertyChanged;
        Session.Messages.CollectionChanged += OnMessagesChanged;
        Session.QueueReturned += OnQueueReturned;
        _apiKey.Changed += OnApiKeyChanged;
        _workspace.Changed += OnWorkspaceChanged;
        Session.LoadLatest();
    }

    public event EventHandler? FocusRequested;

    public ChatSession Session { get; }

    /// <summary>The displayed feed: messages and agent work blocks.</summary>
    public ChatFeed Feed { get; }

    public AgentModeViewModel Mode { get; }

    public ModelSettingsViewModel Model { get; }

    public ContextUsageViewModel Context { get; }

    public ActiveFileContextViewModel ActiveFile { get; }

    public TodoPanelViewModel Todo { get; }

    public ChangesPanelViewModel Changes { get; }

    /// <summary>Files attached to the next message.</summary>
    public ChatAttachmentsViewModel Attachments { get; }

    /// <summary>Messages sent while the agent works; they go to the model at the next step boundary.</summary>
    public QueuedMessagesViewModel Queued { get; }

    public ObservableCollection<ChatMessageViewModel> Messages => Session.Messages;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Input { get; set; } = string.Empty;

    public bool IsBusy => Session.IsBusy;

    public string Title => Session.Title;

    public bool HasMessages => Messages.Count > 0;

    /// <summary>Without a key, the empty chat shows a hint with a "Set key" button.</summary>
    public bool HasApiKey => _apiKey.HasKey;

    /// <summary>Chats are saved in the folder; without one the empty screen shows a hint.</summary>
    public bool HasWorkspace => _workspace.Root is not null;

    /// <summary>The folder's latest chats except the current one, shown on the empty screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentChats))]
    public partial IReadOnlyList<RecentChat> RecentChats { get; private set; } = [];

    public bool HasRecentChats => RecentChats.Count > 0;

    /// <summary>With a key, an empty chat shows what the agent can do and recent chats.</summary>
    public bool IsWelcomeVisible => HasApiKey && !HasMessages;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Session.PropertyChanged -= OnSessionPropertyChanged;
        Session.Messages.CollectionChanged -= OnMessagesChanged;
        Session.QueueReturned -= OnQueueReturned;
        _apiKey.Changed -= OnApiKeyChanged;
        _workspace.Changed -= OnWorkspaceChanged;
        Feed.Dispose();
    }

    // The call lasts the whole turn; a second call during the turn queues the message.
    [RelayCommand(CanExecute = nameof(CanSend), AllowConcurrentExecutions = true)]
    private async Task SendAsync()
    {
        var text = Input.Trim();
        Input = string.Empty;
        await Session.SendAsync(text, ActiveFile.IsAttached, ActiveFile.IsAttached ? ActiveFile.RelativePath : null, Attachments.Take());
    }

    // Not blocked while the agent works: the message is queued (ADR 0026).
    private bool CanSend() => !string.IsNullOrWhiteSpace(Input);

    // After a stop, unsent messages return to the input box ahead of what is already typed.
    private void OnQueueReturned(object? sender, string text) =>
        Input = string.IsNullOrWhiteSpace(Input) ? text : text + Environment.NewLine + Input;

    /// <summary>After hitting the request limit, continues the turn where it stopped.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task ContinueAsync() =>
        Session.SendAsync(ContinueText, ActiveFile.IsAttached, ActiveFile.IsAttached ? ActiveFile.RelativePath : null);

    /// <summary>Executes a Plan-mode plan in Agent mode: same history, all tools.</summary>
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ExecutePlanAsync()
    {
        if (Mode.SetMode(AgentMode.Agent))
        {
            await Session.SendAsync(ExecutePlanText, ActiveFile.IsAttached, ActiveFile.IsAttached ? ActiveFile.RelativePath : null);
        }
    }

    private bool CanContinue() => !IsBusy && HasMessages;

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Stop() => Session.Stop();

    [RelayCommand]
    private void NewChat()
    {
        Context.IsDetailsOpen = false;
        Session.StartNew();
        RequestFocus();
    }

    /// <summary>Lists the folder's saved chats in the palette, newest first; choosing one opens it.</summary>
    [RelayCommand]
    private void ShowHistory()
    {
        var now = _time.GetLocalNow();
        var items = _history.List()
            .Select(chat => new QuickPickItem(chat.Id, chat.Title)
            {
                Detail = chat.Id == Session.CurrentId ? string.Format(CultureInfo.CurrentCulture, Strings.ChatOpenDetail, ChatDates.Describe(chat.Updated, now)) : ChatDates.Describe(chat.Updated, now),
            })
            .ToList();
        _quickPick.Show(new QuickPickProvider(Strings.OpenChatPrompt, items, item => OpenChatAsync(item.Id))
        {
            EmptyText = HasWorkspace ? Strings.NoSavedChats : Strings.OpenFolderForChats,
        });
    }

    [RelayCommand]
    private Task OpenChatAsync(string? id)
    {
        if (id is not null && !Session.Open(id))
        {
            RefreshRecentChats();
        }

        RequestFocus();
        return Task.CompletedTask;
    }

    /// <summary>Moves the open chat to the Recycle Bin and starts a new one.</summary>
    [RelayCommand]
    private void DeleteChat()
    {
        Session.DeleteCurrent();
        RequestFocus();
    }

    [RelayCommand]
    private Task OpenLinkAsync(string? target) => target is null ? Task.CompletedTask : _links.OpenAsync(target);

    /// <summary>Copies the last answer as Markdown: the keyboard path, since the mouse can select feed text.</summary>
    [RelayCommand]
    private void CopyLastAnswer()
    {
        if (Messages.LastOrDefault(message => message.Kind == ChatMessageKind.Assistant) is { } answer)
        {
            _shell.CopyToClipboard(answer.Text);
        }
    }

    [RelayCommand]
    private async Task SetApiKeyAsync() => await _commands.ExecuteAsync(AgentCommandIds.SetApiKey);

    private void RefreshRecentChats()
    {
        var now = _time.GetLocalNow();
        RecentChats =
        [
            .. _history.List()
                .Where(chat => chat.Id != Session.CurrentId)
                .Take(RecentChatsShown)
                .Select(chat => new RecentChat(chat.Id, chat.Title, ChatDates.Describe(chat.Updated, now))),
        ];
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ChatSession.IsBusy):
                OnPropertyChanged(nameof(IsBusy));
                SendCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                ContinueCommand.NotifyCanExecuteChanged();
                ExecutePlanCommand.NotifyCanExecuteChanged();
                OnBusyChanged();
                Feed.OnBusyChanged();
                break;
            case nameof(ChatSession.CurrentId):
                _ = Changes.RefreshAsync();
                break;
            case nameof(ChatSession.Title):
                OnPropertyChanged(nameof(Title));
                RefreshRecentChats();
                break;
        }
    }

    // Revert is disabled while the agent works; after the turn the change list is reloaded.
    private void OnBusyChanged()
    {
        Changes.IsAgentBusy = IsBusy;
        if (!IsBusy)
        {
            _ = Changes.RefreshAsync();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasMessages));
        ContinueCommand.NotifyCanExecuteChanged();
        ExecutePlanCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsWelcomeVisible));
    }

    private void OnApiKeyChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasApiKey));
        OnPropertyChanged(nameof(IsWelcomeVisible));
    }

    // Another folder has its own chats; the current one was already saved after the last answer.
    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasWorkspace));
        Session.LoadLatest();
    }
}
