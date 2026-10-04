using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Commands;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Benchmarks;

/// <summary>
/// Palette filter over 1,000 commands, run on every key press in the input box.
/// </summary>
[MemoryDiagnoser]
public class PaletteFilterBenchmarks
{
    private const int CommandCount = 1_000;
    private const int RecentCount = 20;

    private static readonly string[] Categories = ["Файл", "Правка", "Вид", "Git", "Отладка", "Терминал", "Оформление"];
    private static readonly string[] Verbs = ["Открыть", "Закрыть", "Показать", "Скрыть", "Переключить", "Создать", "Удалить"];
    private static readonly string[] Objects = ["панель", "файл", "терминал", "ветку", "точку останова", "тему", "вкладку"];

    private PaletteCandidate[] _candidates = [];
    private string[] _recent = [];

    [Params("", "пк", "показать панель", "zzz")]
    public string Query { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _candidates = [.. Enumerable.Range(0, CommandCount).Select(CreateCandidate)];
        _recent = [.. Enumerable.Range(0, RecentCount).Select(i => $"command.{i * 7}")];
    }

    [Benchmark]
    public int Filter() => PaletteFilter.Apply(_candidates, Query, _recent).Count;

    private static PaletteCandidate CreateCandidate(int index)
    {
        var title = $"{Verbs[index % Verbs.Length]} {Objects[index / Verbs.Length % Objects.Length]} {index}";
        var command = new CommandDefinition($"command.{index}", title, (_, _) => ValueTask.CompletedTask, Categories[index % Categories.Length]);
        return new PaletteCandidate(command, index % 3 == 0 ? "Ctrl+K Ctrl+X" : null);
    }
}
