using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Agent tools for background commands (ADR 0012), like Cursor's background terminals and Claude Code's background
/// shells: <c>command_output</c> returns new output, optionally waiting for a regex match or exit; <c>stop_command</c>
/// stops a command with its child processes. A <c>run_command</c> that outlives its wait continues in the background.
/// </summary>
public sealed class BackgroundCommandTools(BackgroundCommands commands, IAgentOutputStore outputs) : IAgentToolProvider
{
    public const string CommandOutputName = "command_output";
    public const string StopCommandName = "stop_command";

    /// <summary>Wait time for a pattern when the model doesn't set one.</summary>
    public const int DefaultWaitSeconds = 30;

    public const int MaxWaitSeconds = 300;

    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(10);

    public IEnumerable<AITool> CreateTools() =>
    [
        new ReadOnlyAIFunction(AIFunctionFactory.Create(CommandOutputAsync, CommandOutputName,
            "Returns the status of a background command and its output since the last read. With waitFor (a .NET regex, case-insensitive, e.g. 'Now listening|error'), " +
            "first waits until a new output line matches it, the command exits or timeoutSeconds pass (default 30). Without waitFor, waits up to timeoutSeconds (default 0) for the command to exit.")),
        AIFunctionFactory.Create(StopCommandAsync, StopCommandName,
            "Stops a background command together with its child processes and returns its last output."),
    ];

    private async Task<string> CommandOutputAsync(
        [Description("Background command id from run_command.")] int id,
        [Description("Optional regex: wait until a new output line matches it.")] string? waitFor = null,
        [Description("How long to wait, up to 300 seconds.")] int? timeoutSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var command = commands.Get(id);
        var pattern = string.IsNullOrWhiteSpace(waitFor) ? null : Validate(waitFor);
        var wait = Math.Clamp(timeoutSeconds ?? (pattern is null ? 0 : DefaultWaitSeconds), 0, MaxWaitSeconds);
        if (wait > 0)
        {
            await command.WaitAsync(pattern, TimeSpan.FromSeconds(wait), cancellationToken);
        }

        return outputs.Fit(commands.Report(command, command.ReadNew()), CommandOutputName);
    }

    private async Task<string> StopCommandAsync([Description("Background command id from run_command.")] int id, CancellationToken cancellationToken = default)
    {
        var command = commands.Get(id);
        commands.Stop(id);
        await command.WaitAsync(pattern: null, StopWait, cancellationToken);
        return outputs.Fit(commands.Report(command, command.ReadNew()), StopCommandName);
    }

    private static string Validate(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return pattern;
        }
        catch (ArgumentException exception)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.BackgroundBadPattern, exception.Message));
        }
    }
}
