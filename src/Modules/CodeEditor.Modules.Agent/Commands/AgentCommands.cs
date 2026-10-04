using CodeEditor.Core.Commands;
using CodeEditor.Core.Menus;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Shell.Commands;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.Menus;
using CodeEditor.Shell.Services;
using CodeEditor.Shell.ToolWindows;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Modules.Agent.Commands;

/// <summary>
/// Agent palette commands: API key (typed by the user in a hidden field), chats, model and parameters, context. Each
/// matches a button in the panel. Key commands do not create the chat panel (the key changes via
/// <see cref="ApiKeyState"/>); panel commands show the panel first.
/// </summary>
public sealed class AgentCommands(
    ApiKeyState apiKey,
    IDialogService dialogs,
    StatusBarViewModel statusBar,
    ICommandService commandService,
    ProjectMemory memory,
    ProjectInstructions rules,
    Func<ChatViewModel> chat) : IDisposable
{
    private static string Category => Strings.AgentTitle;
    private const string SettingsGroup = "4_agent";

    private readonly List<IDisposable> _registrations = [];

    public void Register(ICommandRegistry commands, IMenuRegistry menus)
    {
        Add(commands, AgentCommandIds.SetApiKey, Strings.CommandSetApiKey, SetApiKey);
        Add(commands, AgentCommandIds.ClearApiKey, Strings.CommandClearApiKey, ClearApiKey);
        AddPanel(commands, AgentCommandIds.NewChat, Strings.NewChat, panel => panel.NewChatCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.ShowHistory, Strings.CommandShowHistory, panel => panel.ShowHistoryCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.DeleteChat, Strings.CommandDeleteChat, panel => panel.DeleteChatCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.PickMode, Strings.CommandPickMode, panel => panel.Mode.PickModeCommand.Execute(null));
        // No ManageModels/UseSupportedModels commands: the picker is limited to the ModelCatalog models for now.
        AddPanel(commands, AgentCommandIds.PickModel, Strings.CommandPickModel, panel => panel.Model.PickModelCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.PickHelperModel, Strings.CommandPickHelperModel, panel => panel.Model.PickHelperModelCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.PickExplorerModel, Strings.CommandPickExplorerModel, panel => panel.Model.PickExplorerModelCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.AttachFiles, Strings.CommandAttachFiles, panel => panel.Attachments.PickCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.ModelParameters, Strings.CommandModelParameters, panel => panel.Model.ShowParametersCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.ContextDetails, Strings.CommandContextDetails, panel => panel.Context.ShowDetailsCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.ToggleActiveFile, Strings.CommandToggleActiveFile, panel => panel.ActiveFile.ToggleCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.CopyLastAnswer, Strings.CommandCopyLastAnswer, panel => panel.CopyLastAnswerCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.Continue, Strings.CommandContinue, panel => panel.ContinueCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.ExecutePlan, Strings.ExecutePlan, panel => panel.ExecutePlanCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.AcceptChanges, Strings.CommandAcceptChanges, panel => panel.Changes.AcceptCommand.Execute(null));
        AddPanel(commands, AgentCommandIds.RevertChanges, Strings.CommandRevertChanges, panel => panel.Changes.RevertAllCommand.Execute(null));
        _registrations.Add(commands.Register(new CommandDefinition(AgentCommandIds.OpenMemory, Strings.CommandOpenMemory, (_, _) => OpenMemoryAsync(), Category)));
        _registrations.Add(commands.Register(new CommandDefinition(AgentCommandIds.OpenRules, Strings.CommandOpenRules, (_, _) => OpenRulesAsync(), Category)));

        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, AgentCommandIds.SetApiKey, SettingsGroup, title: Strings.MenuApiKey)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, AgentCommandIds.OpenMemory, SettingsGroup, order: 2, title: Strings.MenuOpenMemory)));
        _registrations.Add(menus.Register(MenuItemDefinition.ForCommand(MenuIds.Manage, AgentCommandIds.OpenRules, SettingsGroup, order: 3, title: Strings.MenuOpenRules)));
    }

    /// <summary>Opens folder rules (agent.md), creating it from a template; file problems go to the status bar.</summary>
    private async ValueTask OpenRulesAsync()
    {
        string? path;
        try
        {
            path = rules.EnsureAgentFile();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            statusBar.Message = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.RulesFileFailed, exception.Message);
            return;
        }

        if (path is null)
        {
            statusBar.Message = Strings.RulesNoFolder;
            return;
        }

        await commandService.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
        if (rules.Load().Problems is { Count: > 0 } problems)
        {
            statusBar.Message = RulesProblemReporter.Describe(problems);
        }
    }

    /// <summary>Opens the folder memory index <c>MEMORY.md</c>; notes live next to it (ADR 0012).</summary>
    private async ValueTask OpenMemoryAsync()
    {
        string? path;
        try
        {
            path = memory.EnsureIndexFile();
        }
        catch (Contracts.AgentToolException exception)
        {
            statusBar.Message = exception.Message;
            return;
        }

        if (path is null)
        {
            statusBar.Message = Strings.MemoryNoFolder;
            return;
        }

        await commandService.ExecuteAsync(ShellCommandIds.OpenFile, new OpenFileRequest(path));
    }

    public void Dispose()
    {
        _registrations.ForEach(registration => registration.Dispose());
        _registrations.Clear();
    }

    private void SetApiKey()
    {
        var key = dialogs.PromptSecret(Strings.ApiKeyDialogTitle, apiKey.KeyPrompt(), apiKey.KeyPage);
        if (key is null)
        {
            return;
        }

        apiKey.Set(key);
        statusBar.Message = Strings.ApiKeySaved;
    }

    private void ClearApiKey()
    {
        if (apiKey.HasKey && dialogs.Confirm(Strings.ClearApiKeyQuestion, Strings.ClearApiKeyDetails, Strings.Delete))
        {
            apiKey.Clear();
            statusBar.Message = Strings.ApiKeyDeleted;
        }
    }

    private void Add(ICommandRegistry commands, string id, string title, Action handler) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, (_, _) =>
        {
            handler();
            return ValueTask.CompletedTask;
        }, Category)));

    // Popups and the feed live in the panel, so it is shown and focused first.
    private void AddPanel(ICommandRegistry commands, string id, string title, Action<ChatViewModel> handler) =>
        _registrations.Add(commands.Register(new CommandDefinition(id, title, async (_, _) =>
        {
            await commandService.ExecuteAsync(ToolWindowAreaViewModel.ShowCommandId(AgentModule.ToolWindowId));
            handler(chat());
        }, Category)));
}
