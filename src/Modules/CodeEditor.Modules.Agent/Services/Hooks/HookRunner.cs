using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>
/// Runs user hooks (<see cref="AgentHookSettings"/>) as Claude Code does: each one a PowerShell command in the workspace,
/// with the event as JSON on standard input and <c>BREEZE_HOOK_EVENT</c>, <c>BREEZE_PROJECT_DIR</c> in the environment.
/// Exit code 0 passes (its output is context for a message), 2 blocks with the output as the reason; any other code or
/// a timeout is logged and ignored: a broken hook must not stop the agent.
/// </summary>
public sealed partial class HookRunner(IOptionsMonitor<AgentOptions> options, IProcessRunner runner, IWorkspace workspace, ILogger<HookRunner> logger)
{
    public const int DefaultTimeoutSeconds = 30;
    public const int MaxTimeoutSeconds = 600;
    public const int BlockExitCode = 2;

    /// <summary>Hook output that reaches the model, in characters.</summary>
    public const int MaxOutput = 4_000;

    // UTF-8 both ways, as for agent commands: Windows PowerShell defaults to the OEM code page.
    private const string Prelude =
        "$u = New-Object System.Text.UTF8Encoding $false; [Console]::OutputEncoding = $u; [Console]::InputEncoding = $u; $OutputEncoding = $u; ";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>The event has hooks: building its JSON is worth it.</summary>
    public bool Has(string hookEvent) => Hooks(hookEvent).Count > 0;

    /// <summary>Runs the event's hooks in order, those whose matcher fits <paramref name="tool"/>; a blocking one ends the run.</summary>
    /// <param name="mode">The turn's mode (<c>agent_mode</c>): a hook that runs tests has nothing to say about an answer in Ask mode.</param>
    public async Task<HookOutcome> RunAsync(string hookEvent, AgentMode mode, string? tool, JsonObject input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hooks = Hooks(hookEvent).Where(hook => hook.Command.Length > 0 && Matches(hook, tool)).ToList();
        if (hooks.Count == 0)
        {
            return default;
        }

        input["hook_event_name"] = hookEvent;
        input["cwd"] = workspace.Root;
        input["agent_mode"] = mode.ToString().ToLowerInvariant();
        var json = input.ToJsonString();
        var said = new List<string>();
        foreach (var hook in hooks)
        {
            var result = await RunOneAsync(hookEvent, hook, json, cancellationToken);
            var output = Short(result.Output.Trim());
            if (result is { TimedOut: false, ExitCode: BlockExitCode })
            {
                LogBlocked(logger, hookEvent, hook.Command);
                return new HookOutcome(true, output.Length > 0 ? output : null);
            }

            if (result.TimedOut || result.ExitCode != 0)
            {
                LogFailed(logger, hookEvent, hook.Command, result.ExitCode, result.TimedOut);
            }
            else if (output.Length > 0)
            {
                said.Add(output);
            }
        }

        return new HookOutcome(false, said.Count > 0 ? string.Join('\n', said) : null);
    }

    private IReadOnlyList<AgentHook> Hooks(string hookEvent)
    {
        var hooks = options.CurrentValue.Hooks;
        return hookEvent switch
        {
            HookEvents.PreToolUse => hooks.PreToolUse,
            HookEvents.PostToolUse => hooks.PostToolUse,
            HookEvents.UserPromptSubmit => hooks.UserPromptSubmit,
            HookEvents.Stop => hooks.Stop,
            _ => [],
        };
    }

    // The matcher names whole tools, as in Claude Code: "read_file" doesn't match "read_files".
    private bool Matches(AgentHook hook, string? tool)
    {
        if (tool is null || string.IsNullOrWhiteSpace(hook.Matcher))
        {
            return true;
        }

        try
        {
            return Regex.IsMatch(tool, $"^(?:{hook.Matcher})$", RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException exception)
        {
            LogBadMatcher(logger, hook.Matcher, exception.Message);
            return false;
        }
    }

    private async Task<ProcessResult> RunOneAsync(string hookEvent, AgentHook hook, string json, CancellationToken cancellationToken)
    {
        var folder = workspace.Root ?? Path.GetTempPath();
        var request = new ProcessRequest("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", Prelude + hook.Command], folder)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(hook.TimeoutSeconds ?? DefaultTimeoutSeconds, 1, MaxTimeoutSeconds)),
            Input = json,
            Environment = new Dictionary<string, string> { ["BREEZE_HOOK_EVENT"] = hookEvent, ["BREEZE_PROJECT_DIR"] = folder },
        };
        try
        {
            return await runner.RunAsync(request, onLine: null, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return new ProcessResult(-1, exception.Message, TimedOut: false, TimeSpan.Zero);
        }
    }

    private static string Short(string text) => text.Length <= MaxOutput ? text : string.Concat(text.AsSpan(0, MaxOutput), "…");

    [LoggerMessage(Level = LogLevel.Information, Message = "Hook {Event} blocked: {Command}")]
    private static partial void LogBlocked(ILogger logger, string @event, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hook {Event} failed with exit code {ExitCode} (timed out: {TimedOut}): {Command}")]
    private static partial void LogFailed(ILogger logger, string @event, string command, int exitCode, bool timedOut);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hook matcher '{Matcher}' is not a valid regex: {Reason}")]
    private static partial void LogBadMatcher(ILogger logger, string matcher, string reason);
}
