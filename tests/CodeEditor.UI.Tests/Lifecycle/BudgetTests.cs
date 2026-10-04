using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// M1 budgets on the real window: a 10,000-file folder, a 10 MB file, opening and closing a tab 100 times.
/// </summary>
public sealed partial class BudgetTests(ITestOutputHelper output) : IDisposable
{
    private const int TabCycles = 100;
    private const int WarmUpCycles = 5;
    private const int LargeFolderFiles = 10_000;

    private static readonly TimeSpan IndexSettle = TimeSpan.FromSeconds(3);

    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void TabOpenClose_100Times_ReturnsMemory()
    {
        File.WriteAllText(Path.Combine(_folder, "Program.cs"), string.Concat(Enumerable.Repeat("class Program { static void Main() { } }\r\n", 200)));
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
        session.WaitFor("EditorTab.Program.cs");

        CloseAndReopen(session, WarmUpCycles);
        var before = MeasureHeapKilobytes(session);

        CloseAndReopen(session, TabCycles);
        var after = MeasureHeapKilobytes(session);

        Record($"Куча до {before} КБ, после {TabCycles} циклов {after} КБ");
        Assert.True(after - before <= PerformanceBudgets.TabCyclesHeapGrowthKilobytes,
            $"После {TabCycles} открытий и закрытий куча выросла на {after - before} КБ.");
    }

    [Fact]
    public void LargeFile_10MB_OpensWithinBudget()
    {
        var file = Path.Combine(_folder, "Large.cs");
        WriteLargeFile(file);
        using var session = AppSession.WithArguments(_folder);
        var node = session.WaitFor("Explorer.Node.Large.cs");

        var stopwatch = Stopwatch.StartNew();
        node.GuardedDoubleClick();
        session.WaitFor("EditorTab.Large.cs");
        session.WaitForText("StatusBar.editor.position", text => text.StartsWith("Стр. 1,", StringComparison.Ordinal), PerformanceBudgets.LargeFileOpen * 4);
        stopwatch.Stop();

        Record($"Файл {new FileInfo(file).Length / PerformanceBudgets.Megabyte} МБ открыт за {stopwatch.ElapsedMilliseconds} мс (с задержкой UI Automation)");

        // Exact time from the app log: background read and decode plus buffer creation on the UI thread.
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_U);
        var log = session.WaitForValue(AutomationIds.OutputText, text => OpenedPattern().IsMatch(text), TimeSpan.FromSeconds(10));
        var opened = OpenedPattern().Match(log);
        var total = TimeSpan.FromMilliseconds(int.Parse(opened.Groups[1].Value, CultureInfo.InvariantCulture));
        var ui = TimeSpan.FromMilliseconds(int.Parse(opened.Groups[2].Value, CultureInfo.InvariantCulture));
        Record($"Файл 10 МБ по журналу: {total.TotalMilliseconds} мс, из них UI-поток {ui.TotalMilliseconds} мс");
        Assert.True(total <= PerformanceBudgets.LargeFileOpen, $"Файл 10 МБ открывался {total.TotalMilliseconds} мс.");
        Assert.True(ui <= PerformanceBudgets.UiThreadBlock, $"UI-поток был занят {ui.TotalMilliseconds} мс.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);

        // The window stays responsive: jumping to the end of a 200,000-line file is immediate.
        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
        session.WaitForText("StatusBar.editor.position", text => LineNumber(text) > 100_000, PerformanceBudgets.LargeFileOpen * 4);
    }

    [Fact]
    public async Task Folder_10000Files_StaysWithinMemoryBudget()
    {
        var folder = LargeFolder();
        using var session = AppSession.WithArguments(folder);
        session.WaitFor("Explorer.Node.module0");
        await Task.Delay(IndexSettle, TestContext.Current.CancellationToken);

        // The index is built: quick open finds a deeply nested file.
        session.FocusWindow();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
        session.WaitForFocus(AutomationIds.PaletteQuery);
        Keyboard.Type("Class9999");
        session.WaitFor(AutomationIds.PaletteItem(Path.Combine(folder, "module19", "part49", "Class9999.cs")));
        Keyboard.Type(VirtualKeyShort.ESCAPE);

        using var process = Process.GetProcessById(session.ProcessId);
        var privateBytes = process.PrivateMemorySize64;
        Record($"Папка на {LargeFolderFiles} файлов: приватная память {privateBytes / PerformanceBudgets.Megabyte} МБ, куча {MeasureHeapKilobytes(session) / 1024} МБ");
        Assert.True(privateBytes <= PerformanceBudgets.IdlePrivateMemoryBytes,
            $"Приватная память {privateBytes / PerformanceBudgets.Megabyte} МБ при папке на {LargeFolderFiles} файлов.");
    }

    /// <summary>Writes a measurement to the test output and to <c>screenshots/budgets.txt</c>, to compare runs.</summary>
    private void Record(string measurement)
    {
        output.WriteLine(measurement);
        var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "screenshots")).FullName;
        File.AppendAllText(Path.Combine(folder, "budgets.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm} {measurement}{Environment.NewLine}");
    }

    private static void CloseAndReopen(AppSession session, int cycles)
    {
        for (var i = 0; i < cycles; i++)
        {
            session.WaitFor("TextEditor").GuardedClick();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
            session.WaitUntilGone("EditorTab.Program.cs");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_T);
            session.WaitFor("EditorTab.Program.cs");
        }
    }

    /// <summary>The developer command collects garbage in the app and shows the heap size in the status bar.</summary>
    private static long MeasureHeapKilobytes(AppSession session)
    {
        session.FocusWindow();
        Keyboard.Type(VirtualKeyShort.F1);
        session.WaitForFocus(AutomationIds.PaletteQuery);
        session.TypeInto(AutomationIds.PaletteQuery, "память после");
        session.WaitFor(AutomationIds.PaletteItem("developer.showMemory"));
        Keyboard.Type(VirtualKeyShort.ENTER);
        var message = session.WaitForText(AutomationIds.StatusBarMessage, HeapPattern().IsMatch, TimeSpan.FromSeconds(10));
        return long.Parse(HeapPattern().Match(message).Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static void WriteLargeFile(string path)
    {
        const long targetBytes = 10 * PerformanceBudgets.Megabyte;
        var text = new StringBuilder();
        for (var line = 0; text.Length < targetBytes; line++)
        {
            text.Append(CultureInfo.InvariantCulture, $"    public static int Value{line} = {line}; // строка {line}\r\n");
        }

        File.WriteAllText(path, text.ToString());
    }

    /// <summary>The 10,000-file folder is built once and kept in the temp folder between runs.</summary>
    private static string LargeFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests.Data", "folder-10k");
        var marker = Path.Combine(root, ".complete");
        if (File.Exists(marker))
        {
            return root;
        }

        for (var i = 0; i < LargeFolderFiles; i++)
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, $"module{i % 20}", $"part{i % 50}")).FullName;
            File.WriteAllText(Path.Combine(folder, $"Class{i}.cs"), $"namespace Sample;\r\n\r\npublic sealed class Class{i}\r\n{{\r\n}}\r\n");
        }

        File.WriteAllText(marker, string.Empty);
        return root;
    }

    private static int LineNumber(string position) =>
        int.Parse(LinePattern().Match(position).Groups[1].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"куча (\d+) КБ")]
    private static partial Regex HeapPattern();

    [GeneratedRegex(@"Large\.cs in (\d+) ms, (\d+) ms of it on the UI thread")]
    private static partial Regex OpenedPattern();

    [GeneratedRegex(@"Стр\. (\d+),")]
    private static partial Regex LinePattern();
}
