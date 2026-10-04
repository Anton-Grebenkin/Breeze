using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Approval of agent commands (ADR 0012): read-only commands (<see cref="ReadOnlyCommands"/>) and those matching user
/// rules (<see cref="CommandRules"/>) run without a card; for others the card offers "Always allow", which writes the
/// rule to <c>terminal.allowedCommands</c> in the user's <c>settings.json</c>.
/// </summary>
public sealed partial class CommandApprovals(IOptionsMonitor<TerminalOptions> options, ISettingsService settings, ILogger<CommandApprovals> logger) : IAgentApprovalPolicy
{
    public const string AllowedCommandsKey = TerminalOptions.Section + ".allowedCommands";

    public bool CanDecide(string toolName) => toolName == CommandAgentTools.RunCommandName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Command(arguments) is { } command
        && CommandRules.IsAllowed(CommandTokenizer.Parse(command), options.CurrentValue.AllowedCommands);

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Command(arguments) is { } command && ReadOnlyCommands.IsReadOnly(CommandTokenizer.Parse(command));

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) =>
        CanDecide(toolName) && Command(arguments) is { } command ? CommandRules.Suggest(CommandTokenizer.Parse(command)) : null;

    public void AllowAlways(string toolName, string rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var rules = options.CurrentValue.AllowedCommands;
        if (!CanDecide(toolName) || rules.Contains(rule, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!settings.TrySetUserValue(AllowedCommandsKey, (string[])[.. rules, rule], out var error))
        {
            LogRuleNotSaved(logger, rule, error);
        }
    }

    private static string? Command(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("command", out var value) && value?.ToString() is { Length: > 0 } command ? command : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Command rule '{Rule}' was not saved: {Error}")]
    private static partial void LogRuleNotSaved(ILogger logger, string rule, string error);
}
