using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Core.Modules;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Commands;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Shell;
using CodeEditor.Shell.ToolWindows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodeEditor.Modules.Agent;

/// <summary>
/// Agent module: chat with a model in the secondary side bar (<c>Ctrl+Alt+I</c>), as in VS Code. Agent Framework and OpenAI
/// assemblies load on the first message: the module and its registrations do not touch their types at startup.
/// </summary>
public sealed class AgentModule : IModule
{
    public const string Id = "agent";
    public const string ToolWindowId = "agent";

    private readonly List<IDisposable> _registrations = [];

    public ModuleInfo Info { get; } = new(Id, Strings.AgentTitle) { Dependencies = [ShellModule.Id] };

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSettingsSection<AgentOptions>(AgentOptions.Section);
        services.AddSingleton<ApiKeyState>();
        services.AddSingleton<RulesProblemReporter>();
        services.AddSingleton<ProjectInstructions>();
        services.AddSingleton<ProjectRuleGuard>();
        services.AddSingleton<SystemPrompt>();
        services.AddSingleton<TurnMessageBuilder>();
        services.AddSingleton<PromptCacheKey>();
        services.AddSingleton<IChatClientFactory, OpenAIChatClientFactory>();
        services.AddSingleton<TurnBudget>();
        services.AddSingleton<HelperUsage>();
        services.AddSingleton<CacheWarmup>();
        services.AddSingleton<UserMessageQueue>();
        services.AddSingleton<ContextCompaction>();
        services.AddSingleton<VerificationGate>();
        services.AddSingleton<IAgentVerificationLog>(provider => provider.GetRequiredService<VerificationGate>());
        services.AddSingleton<TurnChecks>();
        services.AddSingleton<AgentFileState>();
        services.AddSingleton<IAgentFileState>(provider => provider.GetRequiredService<AgentFileState>());
        services.AddSingleton<AgentConversation>();
        services.AddSingleton<ApprovalCards>();
        services.AddSingleton<ChatHistoryStore>();
        services.AddSingleton<AgentOutputStore>();
        services.AddSingleton<IAgentOutputStore>(provider => provider.GetRequiredService<AgentOutputStore>());
        services.AddSingleton<EnvironmentTools>();
        services.AddSingleton<WorkspaceSnapshot>();
        services.AddSingleton<ProjectMemory>();
        services.AddSingleton<ChatStartContext>();
        services.AddSingleton<IAgentToolProvider, MemoryAgentTools>();
        services.AddSingleton<WebPageReader>();
        services.AddSingleton<WebSearchClient>();
        services.AddSingleton<WebAgentTools>();
        services.AddSingleton<IAgentToolProvider>(provider => provider.GetRequiredService<WebAgentTools>());
        services.AddSingleton<IAgentChangePreviewer>(provider => provider.GetRequiredService<WebAgentTools>());
        services.AddSingleton<IAgentApprovalPolicy, WebApprovals>();
        services.AddSingleton<IAgentToolPresenter, WebToolPresenter>();
        services.AddSingleton<SubagentRunner>();
        services.AddSingleton<ReadOnlyToolSet>();
        services.AddSingleton<ExploreAgent>();
        services.AddSingleton<TranscriptRecorder>();
        services.AddSingleton<Advisor>();
        services.AddSingleton<ChangeCritic>();
        services.AddSingleton<DeepSupervisor>();
        services.AddSingleton<MemoryExtractor>();
        services.AddSingleton<ChatHistory>();
        services.AddSingleton<ChatLinkOpener>();
        services.AddSingleton<AgentActivityLog>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ContextUsageViewModel>();
        services.AddSingleton<IModelListClient, OpenAIModelListClient>();
        services.AddSingleton<ModelDirectory>();
        services.AddSingleton<ModelManagerViewModel>();
        services.AddSingleton<ModelSettingsViewModel>();
        services.AddSingleton<ActiveFileContextViewModel>();
        services.AddSingleton<AttachmentReader>();
        services.AddSingleton<IAttachmentPicker, EmptyAttachmentPicker>();
        services.AddSingleton<ChatAttachmentsViewModel>();
        services.AddSingleton<QueuedMessagesViewModel>();
        services.AddSingleton<TodoList>();
        services.AddSingleton<UserQuestions>();
        services.AddSingleton<ChatChanges>();
        services.AddSingleton<PendingChangesStore>();
        services.AddSingleton<TodoPanelViewModel>();
        services.AddSingleton<ChangesPanelViewModel>();
        services.AddSingleton<ToolImages>();
        services.AddSingleton<IAgentImages>(provider => provider.GetRequiredService<ToolImages>());
        services.AddSingleton<IAgentToolProvider, ImageAgentTools>();
        services.AddSingleton<IAgentToolPresenter, ImageToolPresenter>();
        services.AddSingleton<IAgentToolProvider, WorkflowAgentTools>();
        services.AddSingleton<IAgentToolPresenter, WorkflowToolPresenter>();
        services.AddSingleton<ToolViews>();
        services.AddSingleton<IAgentContextProvider, TodoAgentContext>();
        services.AddSingleton<IAgentContextProvider, UserChangesAgentContext>();
        services.AddSingleton<AgentModeViewModel>();
        services.AddSingleton(provider => new ChatPanelParts(
            provider.GetRequiredService<AgentModeViewModel>(),
            provider.GetRequiredService<ModelSettingsViewModel>(),
            provider.GetRequiredService<ContextUsageViewModel>(),
            provider.GetRequiredService<ActiveFileContextViewModel>(),
            provider.GetRequiredService<TodoPanelViewModel>(),
            provider.GetRequiredService<ChangesPanelViewModel>(),
            provider.GetRequiredService<ChatAttachmentsViewModel>(),
            provider.GetRequiredService<QueuedMessagesViewModel>()));
        services.AddSingleton(provider => new AgentTurnServices(
            provider.GetRequiredService<AgentConversation>(),
            provider.GetRequiredService<ApprovalCards>(),
            provider.GetRequiredService<ContextUsageViewModel>(),
            provider.GetRequiredService<AgentActivityLog>(),
            provider.GetRequiredService<TurnBudget>(),
            provider.GetRequiredService<AgentFileState>(),
            provider.GetRequiredService<TodoList>(),
            provider.GetRequiredService<UserQuestions>(),
            provider.GetRequiredService<TurnChecks>(),
            provider.GetRequiredService<ContextCompaction>(),
            provider.GetRequiredService<HelperUsage>(),
            provider.GetRequiredService<ToolViews>(),
            provider.GetRequiredService<DeepSupervisor>(),
            provider.GetRequiredService<MemoryExtractor>(),
            provider.GetRequiredService<UserMessageQueue>(),
            provider.GetRequiredService<ToolImages>()));
        services.AddSingleton<ChatSession>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton(provider => new AgentCommands(
            provider.GetRequiredService<ApiKeyState>(),
            provider.GetRequiredService<Shell.Services.IDialogService>(),
            provider.GetRequiredService<Shell.ViewModels.StatusBarViewModel>(),
            provider.GetRequiredService<ICommandService>(),
            provider.GetRequiredService<ProjectMemory>(),
            provider.GetRequiredService<ProjectInstructions>(),
            provider.GetRequiredService<ChatViewModel>));
    }

    public void Contribute(IServiceProvider services)
    {
        // Created now: it restores edits awaiting review when a folder opens, before any chat is shown.
        services.GetRequiredService<PendingChangesStore>();
        _registrations.Add(services.GetRequiredService<IToolWindowRegistry>().Register(new ToolWindowDefinition(
            ToolWindowId, Strings.AgentTitle, IconNames.Chat, ToolWindowLocation.SecondarySideBar, services.GetRequiredService<ChatViewModel>)
        {
            Order = 2,
            Keybinding = "Ctrl+Alt+I",
        }));

        services.GetRequiredService<AgentCommands>().Register(
            services.GetRequiredService<ICommandRegistry>(),
            services.GetRequiredService<IMenuRegistry>());
    }
}
