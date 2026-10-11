using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Turn;
using CodeEditor.Modules.Agent.Services.Watchdog;

namespace CodeEditor.Modules.Agent.Tests.Watchdog;

/// <summary>The turn watchdog: the same error on the same target, a run of failures, a slow test run, checkpoints.</summary>
public sealed class TurnWatchdogTests
{
    private readonly TurnBudget _budget = new();
    private readonly TurnWatchdog _watchdog;

    public TurnWatchdogTests()
    {
        _budget.Begin(limit: 100);
        _watchdog = new TurnWatchdog(_budget);
        _watchdog.Begin();
    }

    [Fact]
    public void SameErrorOnSameFile_AdviceThenStop_ThoughNumbersDiffer()
    {
        var first = _watchdog.After(Failed("apply_patch", Patch("src/A.cs"), "Ошибка: фрагмент «x» встречается 2 раза (строки 105, 152)"));
        var second = _watchdog.After(Failed("apply_patch", Patch("src/A.cs"), "Ошибка: фрагмент «y» встречается 2 раза (строки 17, 230)"));
        var third = _watchdog.After(Failed("apply_patch", Patch("src/A.cs"), "Ошибка: фрагмент «z» встречается 3 раза (строки 1, 2, 3)"));

        Assert.Empty(first);
        Assert.Equal([Format(Strings.WatchdogSameError, "apply_patch", "src/A.cs")], second);
        Assert.Equal([Format(Strings.WatchdogStuck, "apply_patch", "src/A.cs", 3)], third);
    }

    [Fact]
    public void OtherFile_OrSuccessInBetween_StartsOver()
    {
        _watchdog.After(Failed("apply_patch", Patch("src/A.cs"), "Ошибка: не найдено"));
        var otherFile = _watchdog.After(Failed("apply_patch", Patch("src/B.cs"), "Ошибка: не найдено"));
        _watchdog.After(Succeeded("apply_patch", Patch("src/A.cs")));
        var afterSuccess = _watchdog.After(Failed("apply_patch", Patch("src/A.cs"), "Ошибка: не найдено"));

        Assert.Empty(otherFile);
        Assert.Empty(afterSuccess);
    }

    [Fact]
    public void FailureStreak_AsksForAPause_ThenCountsAnew()
    {
        var notes = Enumerable.Range(1, TurnWatchdog.FailureStreak + 1)
            .Select(index => _watchdog.After(Failed("read_file", new() { ["path"] = $"f{index}" }, "Ошибка: нет файла")))
            .ToList();

        Assert.All(notes.Take(TurnWatchdog.FailureStreak - 1), note => Assert.Empty(note));
        Assert.Equal([Format(Strings.WatchdogFailureStreak, TurnWatchdog.FailureStreak)], notes[TurnWatchdog.FailureStreak - 1]);
        Assert.Empty(notes[^1]);
    }

    [Fact]
    public void SlowTestRun_NotedOncePerTurn()
    {
        var slow = TurnWatchdog.SlowRun + TimeSpan.FromSeconds(30);

        var first = _watchdog.After(Succeeded("run_tests", [], slow));
        var command = _watchdog.After(Succeeded("run_command", new() { ["command"] = "dotnet test tests/A.Tests" }, slow));
        _watchdog.Begin();
        var nextTurn = _watchdog.After(Succeeded("run_command", new() { ["command"] = "dotnet test tests/A.Tests" }, slow));

        Assert.Equal([Format(Strings.WatchdogSlowRun, Format(Strings.WatchdogSeconds, 90))], first);
        Assert.Empty(command);
        Assert.Single(nextTurn);
        Assert.Empty(_watchdog.After(Succeeded("build", [], slow)));
    }

    [Fact]
    public void Checkpoint_EveryFortySteps()
    {
        for (var request = 0; request < TurnWatchdog.CheckpointSteps; request++)
        {
            _budget.TryStartRequest();
        }

        var checkpoint = _watchdog.After(Succeeded("read_file", []));
        var next = _watchdog.After(Succeeded("read_file", []));

        Assert.Equal([Format(Strings.WatchdogCheckpoint, TurnWatchdog.CheckpointSteps, 100)], checkpoint);
        Assert.Empty(next);
    }

    [Fact]
    public void Target_FromPatchEditsOrPath()
    {
        Assert.Equal("src/A.cs", ToolTarget.Of(Patch("src/A.cs")));
        Assert.Equal("tests/A.Tests", ToolTarget.Of(new Dictionary<string, object?> { ["project"] = "tests/A.Tests", ["filter"] = "X" }));
        Assert.Equal(string.Empty, ToolTarget.Of(new Dictionary<string, object?> { ["filter"] = "X" }));
    }

    private static Dictionary<string, object?> Patch(string path) =>
        new() { ["input"] = $"*** Begin Patch\n*** Update File: {path}\n@@\n-a\n+b\n*** End Patch" };

    private static ToolCallRecord Failed(string tool, Dictionary<string, object?> arguments, string error) =>
        new(tool, arguments, error, Failed: true, TimeSpan.FromSeconds(1));

    private static ToolCallRecord Succeeded(string tool, Dictionary<string, object?> arguments, TimeSpan? elapsed = null) =>
        new(tool, arguments, "ok", Failed: false, elapsed ?? TimeSpan.FromSeconds(1));

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
