using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Agent.ViewModels.Chat;

/// <summary>Services an agent turn depends on.</summary>
public sealed record AgentTurnServices(
    AgentConversation Conversation,
    ApprovalCards ApprovalCards,
    ContextUsageViewModel Usage,
    AgentActivityLog Log,
    TurnBudget Budget,
    AgentFileState FileState,
    TodoList Todos,
    UserQuestions Questions,
    TurnChecks Checks,
    ContextCompaction Compaction,
    HelperUsage HelperUsage,
    ToolViews ToolViews,
    DeepSupervisor Deep,
    MemoryExtractor MemoryExtractor,
    UserMessageQueue Queue,
    ToolImages Images);
