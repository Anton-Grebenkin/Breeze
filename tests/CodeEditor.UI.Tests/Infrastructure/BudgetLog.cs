using System.Globalization;

namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Measurements of the budget tests, appended to <c>screenshots/budgets.txt</c> next to the test build, to compare
/// runs. The build configuration is part of every line: a Debug number says nothing about a Release budget.
/// </summary>
internal static class BudgetLog
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    public static void Record(string measurement)
    {
        var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "screenshots")).FullName;
        File.AppendAllText(
            Path.Combine(folder, "budgets.txt"),
            string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm} [{Configuration}] {measurement}{Environment.NewLine}"));
    }
}
