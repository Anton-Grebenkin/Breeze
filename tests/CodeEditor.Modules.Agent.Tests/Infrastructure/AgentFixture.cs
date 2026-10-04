using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Attachments;
using CodeEditor.Modules.Agent.Services.Cache;
using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Modules.Agent.Services.Context;
using CodeEditor.Modules.Agent.Services.Conversation;
using CodeEditor.Modules.Agent.Services.Deep;
using CodeEditor.Modules.Agent.Services.History;
using CodeEditor.Modules.Agent.Services.Memory;
using CodeEditor.Modules.Agent.Services.Models;
using CodeEditor.Modules.Agent.Services.Prompts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Composer;
using CodeEditor.Modules.Agent.ViewModels.Models;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Infrastructure;

/// <summary>Chat on a fake model with an API key set and default settings.</summary>
internal sealed class AgentFixture : IDisposable
{
    public AgentFixture()
    {
        // Keys for every service: AITUNNEL is the default; the ProxyAPI key also covers unknown endpoints.
        Secrets.Set(AgentSecrets.AitunnelApiKey, "sk-test");
        Secrets.Set(AgentSecrets.ProvodApiKey, "sk-test");
        Secrets.Set(AgentSecrets.ApiKey, "sk-test");
        ApiKey = new ApiKeyState(Secrets, Options);
        Images = new ToolImages(Options);
        Workspace = new Core.Files.Workspace(FileSystem, new ContextKeyService(), NullLogger<Core.Files.Workspace>.Instance);
        Index = new Core.Files.FileIndex(Workspace, FileSystem, NullLogger<Core.Files.FileIndex>.Instance);
        Memory = new ProjectMemory(Workspace, FileSystem, TimeProvider.System);
        Outputs = new AgentOutputStore(Workspace, FileSystem, TimeProvider.System, NullLogger<AgentOutputStore>.Instance);
        Start = new ChatStartContext(new WorkspaceSnapshot(Workspace, Index, FileSystem, new EnvironmentTools(FileSystem, path: string.Empty)), Memory);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        Changes = new ChatChanges(FileState, Documents, FileSystem, Workspace, new InlineUiDispatcher());
        Gate = new VerificationGate(FileState);
        Compaction = new ContextCompaction(FileState, Todos, Workspace, Start, HelperUsage, Outputs, NullLoggerFactory.Instance);
        Subagents = new SubagentRunner(new Factory(this), FileState, HelperUsage, NullLogger<SubagentRunner>.Instance);
        ReadTools = new ReadOnlyToolSet(Policies);
        Explorer = new ExploreAgent(Subagents, ReadTools, Options, Start);
        var advisor = new Advisor(new Factory(this), Options, Transcript, HelperUsage, NullLogger<Advisor>.Instance);
        Deep = new DeepSupervisor(advisor, new ChangeCritic(Subagents, ReadTools, Options, Changes, NullLogger<ChangeCritic>.Instance), Gate, Budget, FileState, Workspace, Transcript);
        MemoryExtractor = new MemoryExtractor(new Factory(this), Options, Memory, Transcript, HelperUsage, Budget, NullLogger<MemoryExtractor>.Instance);
        Instructions = new ProjectInstructions(Workspace, FileSystem, new UserDataPaths(UserDataRoot));
        Rules = new ProjectRuleGuard(Instructions, Workspace);
        Conversation = new AgentConversation(new Factory(this), Options, ApiKey, new SystemPrompt(Workspace, Instructions), ToolProviders, Budget, Compaction, Explorer, ReadTools, Transcript, Deep, CacheKey, new CacheWarmup(HelperUsage, TimeProvider.System, NullLogger<CacheWarmup>.Instance), Queue, ConversationLog, Rules);
        Reverter = new FakeChangeReverter(FileSystem);
        Checks = new TurnChecks(Gate, Budget, Verifiers, Todos, Deep);
        Editors = new EditorAreaViewModel(Documents, [], [], new DocumentSaver(Documents, new FakeDialogs(), StatusBar), new ContextKeyService(), StatusBar);
        Settings.Changed += (_, _) => ApplyWrittenMode();
        History = CreateHistory();
        Chat = CreateChat(History);
    }

    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    /// <summary>User data folder: personal agent.md lives here.</summary>
    public static readonly string UserDataRoot = Path.GetFullPath(@"C:\userdata");

    public ProjectInstructions Instructions { get; }

    public ProjectRuleGuard Rules { get; }

    public FakeFileSystem FileSystem { get; } = new FakeFileSystem().AddDirectory(Root);

    public Core.Files.Workspace Workspace { get; }

    public Core.Files.FileIndex Index { get; }

    public ProjectMemory Memory { get; }

    /// <summary>Post-turn memory extraction on the test helper model.</summary>
    public MemoryExtractor MemoryExtractor { get; }

    /// <summary>Folder snapshot and note list: the blocks of the chat's first message.</summary>
    public ChatStartContext Start { get; }

    /// <summary>Explorer on the test helper model (<see cref="HelperClient"/>).</summary>
    public ExploreAgent Explorer { get; }

    /// <summary>Subagent loop (explorer, critic) on the test helper model.</summary>
    public SubagentRunner Subagents { get; }

    /// <summary>Read tools for subagents; the agent refreshes them when building its tools.</summary>
    public ReadOnlyToolSet ReadTools { get; }

    /// <summary>The agent's last model request, for the advisor.</summary>
    public TranscriptRecorder Transcript { get; } = new();

    /// <summary>Deep mode advisor checkpoints; the advisor and reviewer use the test helper model.</summary>
    public DeepSupervisor Deep { get; }

    /// <summary>Chat edits: the diff for the reviewer and the changes panel.</summary>
    public ChatChanges Changes { get; }

    public ChatHistory History { get; }

    public MemorySecrets Secrets { get; } = new();

    public ApiKeyState ApiKey { get; }

    /// <summary>Test model: the "other" family edits by exact replacement (apply_edits), like pre-GPT-5 models.</summary>
    public const string TestModel = "test/model";

    // Post-turn memory is off; otherwise each tool turn would call the helper model, whose requests tests count.
    // Edits need a card: approval scenarios check the cards and enable "accept immediately" explicitly.
    public TestOptionsMonitor<AgentOptions> Options { get; } = new(new AgentOptions { Model = TestModel, AutoMemory = false, Approvals = AgentApprovals.Edits });

    public List<IAgentToolProvider> ToolProviders { get; } = [];

    public List<IAgentChangePreviewer> Previewers { get; } = [];

    /// <summary>Module approval policies: which calls skip the card and which rule to suggest.</summary>
    public List<IAgentApprovalPolicy> Policies { get; } = [];

    public List<IAgentContextProvider> ContextProviders { get; } = [];

    /// <summary>Module tool rows; the agent's own presenter covers the plan, question and diff.</summary>
    public List<IAgentToolPresenter> Presenters { get; } = [new WorkflowToolPresenter()];

    public TurnBudget Budget { get; } = new();

    public AgentFileState FileState { get; } = new();

    public TodoList Todos { get; } = new();

    public VerificationGate Gate { get; }

    public TurnChecks Checks { get; }

    /// <summary>Code verifiers; when empty there is nothing to build and the verification gate stays silent.</summary>
    public List<IAgentVerifier> Verifiers { get; } = [];

    /// <summary>Helper model: memory notes and the fallback summary for context compaction.</summary>
    public ScriptedChatClient HelperClient { get; } = new();

    public ContextCompaction Compaction { get; }

    public HelperUsage HelperUsage { get; } = new();

    /// <summary>User messages sent mid-turn.</summary>
    public UserMessageQueue Queue { get; } = new();

    public ToolImages Images { get; }

    public FakeChangeReverter Reverter { get; }

    public UserQuestions Questions { get; } = new(new InlineUiDispatcher());

    public ScriptedChatClient Client { get; private set; } = new();

    /// <summary>Chat cache key; tests pass it to the real client explicitly.</summary>
    public PromptCacheKey CacheKey { get; } = new();

    /// <summary>Replaces the scripted model, e.g. with a real OpenAI client on a fake transport.</summary>
    public IChatClient? Model { get; set; }

    public int ClientsCreated { get; private set; }

    public AgentConversation Conversation { get; }

    public ChatViewModel Chat { get; }

    public CommandService Commands { get; } = new(new CommandRegistry(), new ContextKeyService(), NullLogger<CommandService>.Instance);

    public StatusBarViewModel StatusBar { get; } = new();

    public CollectingLogger<AgentConversation> ConversationLog { get; } = new();

    public CollectingLogger<AgentActivityLog> ActivityLog { get; } = new();

    public FakeSettingsService Settings { get; } = new();

    public FakeQuickPick QuickPick { get; } = new();

    public FakeModelListClient ModelList { get; } = new();

    public UserDataPaths UserData { get; } = new(UserDataRoot);

    /// <summary>Stores long tool outputs in the agent data folder on the fake file system.</summary>
    public AgentOutputStore Outputs { get; }

    public ModelManagerViewModel CreateModelManager() =>
        new(new ModelDirectory(Options, ModelList, FileSystem, UserData, NullLogger<ModelDirectory>.Instance), Options, Settings);

    public FakeSystemShell Shell { get; } = new();

    public EditorAreaViewModel Editors { get; }

    public DocumentService Documents { get; }

    public ChatHistory CreateHistory() =>
        new(new ChatHistoryStore(Workspace, FileSystem, NullLogger<ChatHistoryStore>.Instance), Conversation, TimeProvider.System);

    // Writing agent.mode to the fake settings updates the agent options, like rereading settings.json.
    private void ApplyWrittenMode()
    {
        if (Settings.Written.TryGetValue(AgentModeViewModel.ModeKey, out var value) && Enum.TryParse<AgentMode>(value as string, ignoreCase: true, out var mode) && mode != Options.CurrentValue.Mode)
        {
            Options.Set(new AgentOptions { Mode = mode, Model = Options.CurrentValue.Model, Models = Options.CurrentValue.Models });
        }

        if (Settings.Written.TryGetValue(ModelManagerViewModel.ModelsKey, out var models) && models is string[] list && !list.SequenceEqual(Options.CurrentValue.Models))
        {
            Options.Set(new AgentOptions { Mode = Options.CurrentValue.Mode, Model = Options.CurrentValue.Model, Models = [.. list] });
        }
    }

    /// <summary>Files the user "picks" in the attachment dialog.</summary>
    public FakeAttachmentPicker AttachmentPicker { get; } = new();

    public AttachmentReader Attachments => field ??= new(FileSystem, Workspace, FileState);

    /// <summary>Chat panel on this fixture; a separate history acts like an app restart.</summary>
    public ChatViewModel CreateChat(ChatHistory history)
    {
        var usage = new ContextUsageViewModel(Options);
        var parts = new ChatPanelParts(
            new AgentModeViewModel(Options, Settings, QuickPick, StatusBar),
            new ModelSettingsViewModel(Options, Settings, QuickPick, Commands, StatusBar, CreateModelManager()),
            usage,
            new ActiveFileContextViewModel(Editors, Workspace),
            new TodoPanelViewModel(Todos, new InlineUiDispatcher()),
            new ChangesPanelViewModel(Changes, FileState, Reverter, Documents, new InlineUiDispatcher(), Commands, StatusBar),
            new ChatAttachmentsViewModel(AttachmentPicker, Attachments, StatusBar),
            new QueuedMessagesViewModel(Queue, new InlineUiDispatcher()));
        var services = new AgentTurnServices(Conversation, new ApprovalCards(Previewers, Policies, Rules), usage, new AgentActivityLog(ActivityLog), Budget, FileState, Todos, Questions, Checks, Compaction, HelperUsage, new ToolViews(Presenters, NullLogger<ToolViews>.Instance), Deep, MemoryExtractor, Queue, Images);
        var session = new ChatSession(services, history, Options, new TurnMessageBuilder(ContextProviders, Start, Attachments, Options, TimeProvider.System, NullLogger<TurnMessageBuilder>.Instance), new InlineUiDispatcher());
        var links = new ChatLinkOpener(Workspace, FileSystem, Commands, Shell, StatusBar);
        return new ChatViewModel(session, history, parts, links, QuickPick, Shell, Workspace, ApiKey, Commands, TimeProvider.System);
    }

    public void Dispose()
    {
        Deep.Dispose();
        Gate.Dispose();
        Chat.Dispose();
        Conversation.Dispose();
        Index.Dispose();
        Workspace.Dispose();
    }

    public async Task SendAsync(string text)
    {
        Chat.Input = text;
        await Chat.SendCommand.ExecuteAsync(null);
    }

    public sealed class MemorySecrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];

        public string? Get(string name) => _values.GetValueOrDefault(name);

        public void Set(string name, string value) => _values[name] = value;

        public void Remove(string name) => _values.Remove(name);
    }

    private sealed class Factory(AgentFixture fixture) : IChatClientFactory
    {
        public IChatClient Create(AgentOptions options)
        {
            if (fixture.Secrets.Get(AgentServices.SecretFor(options.Endpoint)) is null)
            {
                throw new AgentConfigurationException("Не задан ключ API.");
            }

            // Helper model (summary, explorer): its options are built separately from the agent's.
            if (!ReferenceEquals(options, fixture.Options.CurrentValue))
            {
                return fixture.HelperClient;
            }

            fixture.ClientsCreated++;
            return fixture.Model ?? fixture.Client;
        }
    }
}
