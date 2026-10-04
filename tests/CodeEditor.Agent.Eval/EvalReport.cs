using System.Globalization;
using System.Text;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Markdown run report: a model × service × mode summary (solved share, average hidden-test score, tests added,
/// requests, calls, tokens, time, cost and rubles per solved task) and a row per task with the failure reason.
/// Manually reviewed tasks are excluded from the solved share.
/// </summary>
internal static class EvalReport
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(IReadOnlyList<EvalResult> results, bool dryRun)
    {
        var text = new StringBuilder("# Бенчмарк агента\n\n");
        text.Append(Russian, $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}. Задач: {results.Select(result => result.Task).Distinct().Count()}.");
        text.Append(dryRun ? " Пробный прогон без модели: проверяется обвязка, задачи не решаются.\n\n" : "\n\n");
        text.Append("| Модель | Сервис | Режим | Решено | Балл | Тестов + | Запросов | Вызовов | Вход, ток. | Из кэша | Выход, ток. | Время, с | Стоимость, ₽ | ₽ за решённую |\n");
        text.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var group in results.GroupBy(result => (result.Model, result.Service, result.Mode)))
        {
            AppendSummary(text, $"{group.Key.Model} | {group.Key.Service}", group.Key.Mode, [.. group]);
        }

        text.Append("\n| Задача | Модель | Сервис | Режим | Итог | Балл | Тестов + | Запросов | Вход, ток. | ₽ | Папка |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var result in results)
        {
            var outcome = (result.Manual ? "◌ " : result.Solved ? "✓ " : "✗ ") + result.Details + (result.HitStepLimit ? " (лимит шагов)" : string.Empty);
            text.Append(Russian, $"| {result.Task} | {result.Model} | {result.Service} | {result.Mode} | {outcome} | {Score(result.Score)} | {result.TestsAdded} ");
            text.Append(Russian, $"| {result.Requests} | {result.InputTokens:N0} | {Money(result.Cost)} | {Path.GetFileName(result.Folder)} |\n");
        }

        return text.ToString();
    }

    /// <param name="model">Model and service, as two columns.</param>
    private static void AppendSummary(StringBuilder text, string model, string mode, List<EvalResult> group)
    {
        var graded = group.Where(result => !result.Manual).ToList();
        var solved = graded.Count(result => result.Solved);
        var scores = group.Where(result => result.Score is not null).Select(result => result.Score!.Value).ToList();
        decimal? cost = group.All(result => result.Cost is not null) ? group.Sum(result => result.Cost!.Value) : null;
        text.Append(Russian, $"| {model} | {mode} | {solved} из {graded.Count} | {Score(scores.Count == 0 ? null : scores.Average())} | {group.Average(result => result.TestsAdded):0.#} ");
        text.Append(Russian, $"| {group.Average(result => result.Requests):0.#} | {group.Average(result => result.ToolCalls):0.#} ");
        text.Append(Russian, $"| {group.Average(result => result.InputTokens):N0} | {group.Average(result => result.CachedInputTokens):N0} | {group.Average(result => result.OutputTokens):N0} ");
        text.Append(Russian, $"| {group.Average(result => result.Elapsed.TotalSeconds):0} | {Money(cost)} | {Money(solved == 0 ? null : cost / solved)} |\n");
    }

    private static string Money(decimal? value) => value is { } money ? money.ToString("0.00", Russian) : "—";

    private static string Score(double? value) => value is { } score ? score.ToString("P0", Russian) : "—";
}
