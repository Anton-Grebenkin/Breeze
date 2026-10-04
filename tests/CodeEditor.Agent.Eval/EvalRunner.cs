using System.Diagnostics;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.ViewModels.Chat;
using CodeEditor.Modules.Agent.ViewModels.Composer;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Build;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Runs one task in one mode on one model: a fresh repository and container, an agent turn with the auto-responder
/// and a timeout, then checks and metrics. The run folder stays, with the repository and chat history for analysis.
/// </summary>
internal sealed class EvalRunner(EvalOptions options, string apiKey, string root)
{
    public async Task<EvalResult> RunAsync(EvalTask task, string model, string mode, int run, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(root, $"{task.Id}-{mode}-{Sanitize(model)}-{run}");
        var repo = EvalRepository.Create(RepoFolder(folder, task, mode), task);
        var originals = task.Unchanged.ToDictionary(file => file, file => File.ReadAllText(Path.Combine(repo, file)), StringComparer.Ordinal);
        var testsBefore = EvalRepository.CountTests(repo, task);
        var cursor = mode == CursorPlayer.Mode;
        EvalHost.WriteSettings(Path.Combine(folder, "user"), model, cursor ? "agent" : mode, options);

        // Cursor has its own agent: our services only run the checks (build and tests) and need no model.
        using var dispatcher = new EvalDispatcher();
        await using var services = EvalHost.Build(Path.Combine(folder, "user"), dispatcher, apiKey, options.DryRun || cursor, options.Endpoint);
        await dispatcher.InvokeAsync(() => services.GetRequiredService<IWorkspace>().Open(repo));
        await services.GetRequiredService<IFileIndex>().WhenReady;

        var (result, answer) = cursor
            ? await new CursorPlayer(options).PlayAsync(repo, folder, task, cancellationToken)
            : await PlayAsync(services, dispatcher, task, model, mode, cancellationToken);

        // Agent edits live in tabs; without saving, the checks below would read stale code from disk.
        await services.GetRequiredService<SaveBeforeRun>().SaveAsync(cancellationToken);
        var testsAdded = EvalRepository.CountTests(repo, task) - testsBefore;
        var verdict = await new EvalVerifier(services.GetRequiredService<DotNetBuild>(), services.GetRequiredService<DotNetTests>(), repo)
            .VerifyAsync(task, answer, originals, cancellationToken);
        return result.WithVerdict(verdict, task.ManualReview) with { Model = model, Service = options.Service, Mode = mode, Folder = folder, TestsAdded = testsAdded };
    }

    /// <returns>Turn metrics and the agent's last answer (checked by question tasks).</returns>
    private async Task<(EvalResult Result, string Answer)> PlayAsync(ServiceProvider services, EvalDispatcher dispatcher, EvalTask task, string model, string mode, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<ChatSession>();
        using var responder = new AutoResponder(session.Messages);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        await using var stop = timeout.Token.Register(() => dispatcher.Post(session.Stop));
        var usageView = services.GetRequiredService<ContextUsageViewModel>();
        using var budget = new TaskBudgetWatch(model, options.Service, options.MaxTaskCost, () => usageView.Usage, () => dispatcher.Post(session.Stop));
        var clock = Stopwatch.StartNew();
        TimeSpan? followUpElapsed = null;
        await dispatcher.RunAsync(async () =>
        {
            session.StartNew();
            if (task.FollowUp is not { } followUp)
            {
                await session.SendAsync(task.Prompt, includeActiveEditor: false, attachment: null);
                return;
            }

            await SwitchModeAsync(services, AgentModes.SettingValue(AgentMode.Ask));
            await session.SendAsync(task.Prompt, includeActiveEditor: false, attachment: null);
            await SwitchModeAsync(services, mode);
            if (options.FollowUpPause is { } pause)
            {
                // Timed out during the pause: the turn is already stopped, so skip the follow-up.
                await Task.Delay(pause, timeout.Token).ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
                if (timeout.IsCancellationRequested)
                {
                    return;
                }
            }

            var second = Stopwatch.StartNew();
            await session.SendAsync(followUp, includeActiveEditor: false, attachment: null);
            followUpElapsed = second.Elapsed;
        });

        var usage = usageView.Usage;
        var messages = session.Messages;
        var answer = messages.LastOrDefault(message => message.Kind == ChatMessageKind.Assistant)?.Text ?? string.Empty;

        var error = messages.LastOrDefault(message => message.Kind == ChatMessageKind.Error)?.Text.ReplaceLineEndings(" ");
        var result = new EvalResult(string.Empty, string.Empty, task.Id, Solved: false, string.Empty)
        {
            TurnError = budget.Exceeded ? TaskBudgetWatch.ExceededError : error,
            Requests = usage.Requests,
            ToolCalls = messages.Count(message => message.Kind == ChatMessageKind.Tool),
            InputTokens = usage.TotalInputTokens,
            CachedInputTokens = usage.TotalCachedInputTokens,
            OutputTokens = usage.TotalOutputTokens,
            Elapsed = clock.Elapsed,
            FollowUpElapsed = followUpElapsed,
            HitStepLimit = messages.Any(message => message.Kind == ChatMessageKind.Notice),
        };
        return (result, answer);
    }

    // Switches the mode like the composer's picker: the setting is saved and reread before the next request.
    private static async Task SwitchModeAsync(ServiceProvider services, string mode)
    {
        if (!services.GetRequiredService<ISettingsService>().TrySetUserValue(AgentModeViewModel.ModeKey, mode, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var options = services.GetRequiredService<IOptionsMonitor<AgentOptions>>();
        for (var attempt = 0; attempt < 50 && AgentModes.SettingValue(options.CurrentValue.Mode) != mode; attempt++)
        {
            await Task.Delay(100);
        }
    }

    // External clones go next to the source on a short path: under %TEMP%, deep real-world projects exceeded the
    // 260-character limit. The location is written to repo.txt in the run folder.
    private static string RepoFolder(string folder, EvalTask task, string mode)
    {
        if (task.SourceRepo is not { } source)
        {
            return Path.Combine(folder, "repo");
        }

        Directory.CreateDirectory(folder);
        var repo = Path.Combine(Path.GetDirectoryName(source)!, "runs", $"{DateTime.Now:MMdd-HHmmss}-{mode}");
        File.WriteAllText(Path.Combine(folder, "repo.txt"), repo);
        return repo;
    }

    public static string Sanitize(string model) => string.Concat(model.Select(character => char.IsLetterOrDigit(character) || character is '-' or '.' ? character : '_'));
}
