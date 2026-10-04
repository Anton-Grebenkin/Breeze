using CodeEditor.Core.Processes;

namespace CodeEditor.Core.Tests;

/// <summary>Real Windows processes (cmd.exe): output, exit code, timeout kill, cancellation, closed input.</summary>
public sealed class ProcessRunnerTests
{
    private static readonly string Folder = Path.GetTempPath();

    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task Output_AndExitCode_AreCaptured()
    {
        var lines = new List<string>();

        var result = await _runner.RunAsync(Cmd("echo first & echo second 1>&2 & exit /b 3"), line => { lock (lines) { lines.Add(line); } }, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("first", result.Output, StringComparison.Ordinal);
        Assert.Contains("second", result.Output, StringComparison.Ordinal);
        Assert.Equal(2, lines.Count(line => line.Trim().Length > 0));
    }

    [Fact]
    public async Task Timeout_KillsProcess()
    {
        var result = await _runner.RunAsync(Cmd("ping -n 30 127.0.0.1 >nul") with { Timeout = TimeSpan.FromMilliseconds(500) }, null, TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.True(result.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Cancellation_KillsProcessAndThrows()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _runner.RunAsync(Cmd("ping -n 30 127.0.0.1 >nul"), null, cancellation.Token));
    }

    [Fact]
    public async Task Input_IsClosed_SoPromptsDoNotHang()
    {
        var result = await _runner.RunAsync(Cmd("set /p answer=Answer: & echo done") with { Timeout = TimeSpan.FromSeconds(10) }, null, TestContext.Current.CancellationToken);

        Assert.False(result.TimedOut);
        Assert.Contains("done", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingProgram_IsExplained() =>
        Assert.Contains("Не удалось запустить", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _runner.RunAsync(new ProcessRequest("нет-такой-программы.exe", [], Folder), null, TestContext.Current.CancellationToken))).Message, StringComparison.Ordinal);

    private static ProcessRequest Cmd(string command) => new("cmd.exe", ["/d", "/c", command], Folder);
}
