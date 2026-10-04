using System.Globalization;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Command-line options: <c>--models a/b,c/d --modes agent,ask --tasks find,fix-divide --runs 1 --timeout 600
/// --endpoint URL --dry-run</c>. Defaults: the default model, the agent mode and all tasks. <c>--report dir,dir</c>
/// merges <c>results.json</c> of past runs without running; <c>--recheck dir,dir</c> re-verifies their repositories
/// without a model.
/// </summary>
internal sealed record EvalOptions
{
    public IReadOnlyList<string> Models { get; init; } = [CodeEditor.Modules.Agent.Services.Settings.AgentOptions.DefaultModel];

    public IReadOnlyList<string> Modes { get; init; } = ["agent"];

    /// <summary>Empty means all tasks.</summary>
    public IReadOnlyList<string> Tasks { get; init; } = [];

    public int Runs { get; init; } = 1;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(10);

    public string Endpoint { get; init; } = CodeEditor.Modules.Agent.Services.Settings.AgentOptions.DefaultEndpoint;

    /// <summary>Service title for the endpoint (prices, report column); the endpoint itself if unknown.</summary>
    public string Service => CodeEditor.Modules.Agent.Services.Api.AgentServices.For(Endpoint)?.Title ?? Endpoint;

    /// <summary>Advisor model for Deep mode; <c>null</c> uses the agent model (ADR 0012).</summary>
    public string? AdvisorModel { get; init; }

    /// <summary>No model: a stub answers "done" to test the harness at no cost.</summary>
    public bool DryRun { get; init; }

    /// <summary>Past run folders to merge into one report; empty for a regular run.</summary>
    public IReadOnlyList<string> ReportFrom { get; init; } = [];

    /// <summary>Past run folders to re-verify without a model; empty for a regular run.</summary>
    public IReadOnlyList<string> Recheck { get; init; } = [];

    /// <summary>Spend cap for the whole run in rubles: the next task doesn't start if it could exceed it.</summary>
    public decimal? Budget { get; init; }

    /// <summary>Spend cap per task in rubles: the turn stops as soon as it's exceeded.</summary>
    public decimal? MaxTaskCost { get; init; }

    /// <summary>Cursor CLI model for the <c>cursor</c> mode (see <c>cursor-agent models</c>).</summary>
    public string CursorModel { get; init; } = "auto";

    /// <summary>External task folders (<c>task.json</c> plus a prompt), run on clones of real repositories.</summary>
    public IReadOnlyList<string> External { get; init; } = [];

    /// <summary>Editor UI language (<c>en</c>, <c>ru</c>) for harness texts; <c>null</c> for the system one.</summary>
    public string? UiLanguage { get; init; }

    /// <summary>Agent reasoning effort (<c>low</c>, <c>medium</c>, <c>high</c>); <c>null</c> for the default.</summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// API protocol (<c>Responses</c>, <c>ChatCompletions</c>) as in <c>agent.api</c>; <c>null</c> picks by model.
    /// </summary>
    public string? Api { get; init; }

    /// <summary>Model traffic log (<c>agent.trafficLog</c>) in the run's log folder.</summary>
    public bool Trace { get; init; }

    /// <summary>Folder to print the system prompt and tools for; <c>null</c> for a regular run.</summary>
    public string? ShowPrompt { get; init; }

    /// <summary>
    /// Pause before the follow-up message (<see cref="EvalTask.FollowUp"/>) to test the cache after idle time, e.g. the
    /// one-hour warmup (ADR 0022). It counts toward the turn time, so raise <c>--timeout</c>.
    /// </summary>
    public TimeSpan? FollowUpPause { get; init; }

    /// <exception cref="ArgumentException">Unknown option or missing value.</exception>
    public static EvalOptions Parse(IReadOnlyList<string> args)
    {
        var options = new EvalOptions();
        for (var index = 0; index < args.Count; index++)
        {
            string Value() => index + 1 < args.Count ? args[++index] : throw new ArgumentException($"У {args[index]} нет значения.");
            options = args[index] switch
            {
                "--models" => options with { Models = List(Value()) },
                "--modes" => options with { Modes = List(Value()) },
                "--tasks" => options with { Tasks = List(Value()) },
                "--runs" => options with { Runs = int.Parse(Value(), CultureInfo.InvariantCulture) },
                "--timeout" => options with { Timeout = TimeSpan.FromSeconds(int.Parse(Value(), CultureInfo.InvariantCulture)) },
                "--endpoint" => options with { Endpoint = Value() },
                "--advisor-model" => options with { AdvisorModel = Value() },
                "--dry-run" => options with { DryRun = true },
                "--report" => options with { ReportFrom = List(Value()) },
                "--recheck" => options with { Recheck = List(Value()) },
                "--budget" => options with { Budget = decimal.Parse(Value(), CultureInfo.InvariantCulture) },
                "--max-task-cost" => options with { MaxTaskCost = decimal.Parse(Value(), CultureInfo.InvariantCulture) },
                "--cursor-model" => options with { CursorModel = Value() },
                "--external" => options with { External = List(Value()) },
                "--ui-language" => options with { UiLanguage = Value() },
                "--show-prompt" => options with { ShowPrompt = Value() },
                "--reasoning" => options with { Reasoning = Value() },
                "--api" => options with { Api = Value() },
                "--trace" => options with { Trace = true },
                "--pause" => options with { FollowUpPause = TimeSpan.FromSeconds(int.Parse(Value(), CultureInfo.InvariantCulture)) },
                var unknown => throw new ArgumentException($"Неизвестный параметр {unknown}."),
            };
        }

        return options;
    }

    private static string[] List(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
