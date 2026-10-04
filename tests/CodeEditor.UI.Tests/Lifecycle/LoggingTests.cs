using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// The log file in the real app: created in the data folder at startup, opens in a tab while being written, records
/// executed commands and a shutdown line.
/// </summary>
public sealed class LoggingTests : IDisposable
{
    private const string OpenLogCommand = "developer.openLog";

    private readonly string _userData = AppSession.CreateUserDataFolder();

    public void Dispose() => AppSession.DeleteQuietly(_userData);

    [Fact]
    public void LogFile_OpensInEditor_AndRecordsSession()
    {
        using (var session = AppSession.WithUserData(_userData))
        {
            session.FocusWindow();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            session.WaitForFocus(AutomationIds.PaletteQuery);
            session.TypeInto(AutomationIds.PaletteQuery, "открыть журнал");
            session.WaitFor(AutomationIds.PaletteItem(OpenLogCommand)).GuardedClick();

            var tab = session.WaitFor($"EditorTab.{Path.GetFileName(CurrentLog())}");
            Assert.NotNull(tab);
            session.SaveScreenshot("log-open");
        }

        var text = AppSession.ReadLog(_userData);
        Assert.StartsWith("========", text, StringComparison.Ordinal);
        Assert.Contains("Program: Starting version", text, StringComparison.Ordinal);
        Assert.Contains("StartupReporter: Window rendered", text, StringComparison.Ordinal);
        Assert.Contains($"CommandService: Command {OpenLogCommand} executed", text, StringComparison.Ordinal);
        Assert.Contains("Program: Exited with code 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[ERR]", text, StringComparison.Ordinal);
    }

    private string CurrentLog() =>
        Directory.GetFiles(Path.Combine(_userData, "logs"), "codeeditor-*.log").OrderByDescending(File.GetLastWriteTimeUtc).First();
}
