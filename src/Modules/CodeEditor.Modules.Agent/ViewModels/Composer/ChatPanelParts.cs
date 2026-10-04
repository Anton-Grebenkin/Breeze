namespace CodeEditor.Modules.Agent.ViewModels.Composer;

/// <summary>View models of the panel parts around the input box.</summary>
public sealed record ChatPanelParts(AgentModeViewModel Mode, ModelSettingsViewModel Model, ContextUsageViewModel Context, ActiveFileContextViewModel ActiveFile, TodoPanelViewModel Todo, ChangesPanelViewModel Changes, ChatAttachmentsViewModel Attachments, QueuedMessagesViewModel Queued);
