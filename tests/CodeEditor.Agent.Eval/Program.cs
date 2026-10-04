using System.Globalization;
using System.Text;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Services.Api;
using CodeEditor.Modules.Agent.Services.Models;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Agent benchmark: tasks × models × modes × runs, one at a time. The API key comes from <c>CODEEDITOR_AGENT_KEY</c>
/// or the editor's secret store (read-only). The report is <c>report.md</c> in the run folder under the temp
/// directory. Without a model: <c>--report</c> merges past runs, <c>--recheck</c> re-verifies them after fixing hidden
/// tests or checks.
/// </summary>
internal static class Program
{
    public const string KeyVariable = "CODEEDITOR_AGENT_KEY";

    /// <summary>Keeps the path of a task's work folder under 260 characters.</summary>
    private const int MaxRunNameLength = 48;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var options = EvalOptions.Parse(args);
        if (options.UiLanguage is { } language)
        {
            // Same as the editor's "workbench.language": the UI in one language, requests in another.
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        }

        if (options.ShowPrompt is { } folder)
        {
            await PromptDump.WriteAsync(options, folder);
            return 0;
        }

        if (options.ReportFrom.Count > 0)
        {
            Console.WriteLine(EvalReport.Format(await LoadAsync(options.ReportFrom), dryRun: false));
            return 0;
        }

        if (options.Recheck.Count > 0)
        {
            await RecheckAsync(options);
            return 0;
        }

        var apiKey = options.DryRun ? "dry-run" : Environment.GetEnvironmentVariable(KeyVariable)
            ?? new DpapiSecretStore(new PhysicalFileSystem(), new UserDataPaths()).Get(AgentServices.SecretFor(options.Endpoint));
        if (apiKey is null)
        {
            await Console.Error.WriteLineAsync($"Нет ключа API: задайте его в редакторе или в переменной {KeyVariable}.");
            return 1;
        }

        await RunAsync(options, apiKey);
        return 0;
    }

    private static async Task RunAsync(EvalOptions options, string apiKey)
    {
        // Model names in the folder keep parallel runs of different models from overwriting each other's reports. The
        // name stays short: Windows can't start processes in a folder whose path exceeds 260 characters (error 267).
        var name = EvalRunner.Sanitize(string.Join("+", options.Models.Select(ModelVendors.ShortName)) + "-" + string.Join("+", options.Modes));
        if (name.Length > MaxRunNameLength)
        {
            name = $"{name[..MaxRunNameLength]}-{options.Models.Count}models";
        }

        var root = Path.Combine(Path.GetTempPath(), "CodeEditor.Eval", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + name);
        var tasks = AllTasks(options).Where(task => options.Tasks.Count == 0 || options.Tasks.Contains(task.Id)).ToList();
        var runner = new EvalRunner(options, apiKey, root);
        var results = new List<EvalResult>();
        var spent = 0m;
        foreach (var model in options.Models)
        {
            foreach (var mode in options.Modes)
            {
                foreach (var task in tasks)
                {
                    for (var run = 1; run <= options.Runs; run++)
                    {
                        // The next task may cost up to the per-task cap: stop if that could exceed the budget.
                        if (options.Budget is { } budget && spent + (options.MaxTaskCost ?? 0) > budget)
                        {
                            Console.WriteLine($"Бюджет {budget} ₽: потрачено {spent:0.00} ₽, следующая задача может выйти за него — прогон остановлен.");
                            goto Report;
                        }

                        var result = await runner.RunAsync(task, model, mode, run, CancellationToken.None);
                        results.Add(result);
                        spent += result.Cost ?? 0;
                        Console.WriteLine(Line(result, $"{task.Id} #{run}"));
                        await SaveAsync(root, results, options.DryRun);
                    }
                }
            }
        }

    Report:
        Console.WriteLine();
        Console.WriteLine(EvalReport.Format(results, options.DryRun));
        Console.WriteLine($"Отчёт и папки прогонов: {root}");
    }

    // Keeps the original results in results.before-recheck.json; a repeated recheck doesn't overwrite them.
    private static async Task RecheckAsync(EvalOptions options)
    {
        var tasks = AllTasks(options).ToDictionary(task => task.Id, StringComparer.Ordinal);
        var all = new List<EvalResult>();
        foreach (var root in options.Recheck)
        {
            var before = await LoadAsync([root]);
            var original = Path.Combine(root, "results.before-recheck.json");
            if (!File.Exists(original))
            {
                File.Copy(Path.Combine(root, "results.json"), original);
            }

            var results = new List<EvalResult>();
            foreach (var result in before)
            {
                var rechecked = await EvalRechecker.RecheckAsync(result, tasks[result.Task], CancellationToken.None);
                results.Add(rechecked);
                Console.WriteLine(Line(rechecked, Path.GetFileName(rechecked.Folder)));
            }

            await SaveAsync(root, results, dryRun: false);
            all.AddRange(results);
        }

        Console.WriteLine();
        Console.WriteLine(EvalReport.Format(all, dryRun: false));
    }

    private static IEnumerable<EvalTask> AllTasks(EvalOptions options) =>
        [.. EvalTasks.All(EvalRepository.DivideLine()), .. ShopTasks.All(), .. ShelfTasks.All(), .. ExternalTasks.Load(options.External)];

    private static string Line(EvalResult result, string name) => string.Create(CultureInfo.InvariantCulture,
        $"{result.Model} · {result.Mode} · {name}: {result.Details}{(result.Score is { } score ? $", балл {score:P0}" : string.Empty)} ({result.Requests} запросов, {result.InputTokens + result.OutputTokens} токенов, {result.Cost:0.00} ₽, {result.Elapsed.TotalSeconds:0} с)");

    // Called after each task so an interrupted run still leaves a report; the JSON is for before/after comparisons.
    private static async Task SaveAsync(string root, List<EvalResult> results, bool dryRun)
    {
        await File.WriteAllTextAsync(Path.Combine(root, "report.md"), EvalReport.Format(results, dryRun));
        await File.WriteAllTextAsync(Path.Combine(root, "results.json"), JsonSerializer.Serialize(results, JsonOptions));
    }

    private static async Task<List<EvalResult>> LoadAsync(IEnumerable<string> roots)
    {
        var results = new List<EvalResult>();
        foreach (var root in roots)
        {
            await using var json = File.OpenRead(Path.Combine(root, "results.json"));
            results.AddRange(await JsonSerializer.DeserializeAsync<List<EvalResult>>(json, JsonOptions) ?? []);
        }

        return results;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
