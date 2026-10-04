using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.UI.Tests.Infrastructure;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>App startup: the window opens, startup time and memory stay within budget.</summary>
public sealed partial class StartupTests(AppSession session) : IClassFixture<AppSession>
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    [Fact]
    public void MainWindow_ReportsStartupTimeWithinBudget()
    {
        var message = WaitForStartupMessage();

        var milliseconds = long.Parse(StartupTimePattern().Match(message).Groups[1].Value, CultureInfo.InvariantCulture);
        var startup = TimeSpan.FromMilliseconds(milliseconds);

        Assert.True(startup <= PerformanceBudgets.ColdStart,
            $"Старт занял {milliseconds} мс, бюджет {PerformanceBudgets.ColdStart.TotalMilliseconds} мс.");
    }

    [Fact]
    public async Task IdleApplication_StaysWithinMemoryBudget()
    {
        WaitForStartupMessage();
        await Task.Delay(IdleDelay, TestContext.Current.CancellationToken);

        using var process = Process.GetProcessById(session.ProcessId);
        var privateBytes = process.PrivateMemorySize64;

        Assert.True(privateBytes <= PerformanceBudgets.IdlePrivateMemoryBytes,
            $"Приватная память {privateBytes / PerformanceBudgets.Megabyte} МБ, " +
            $"бюджет {PerformanceBudgets.IdlePrivateMemoryBytes / PerformanceBudgets.Megabyte} МБ.");
    }

    private string WaitForStartupMessage() =>
        session.WaitForText(AutomationIds.StatusBarMessage, StartupTimePattern().IsMatch, RenderTimeout);

    [GeneratedRegex(@"(\d+)\s*мс")]
    private static partial Regex StartupTimePattern();
}
