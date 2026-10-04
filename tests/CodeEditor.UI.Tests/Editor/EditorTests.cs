using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// Editor: a file from the explorer opens in a tab, an edit marks the tab, Ctrl+S writes to disk, Ctrl+W closes.
/// </summary>
public sealed class EditorTests : IDisposable
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(5);

    private readonly string _folder;
    private readonly string _file;

    public EditorTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        _file = Path.Combine(_folder, "Program.cs");
        File.WriteAllText(_file, "class Program\r\n{\r\n    static void Main() { }\r\n}\r\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void OpenEditSaveClose()
    {
        using var session = AppSession.WithArguments(_folder);

        session.WaitFor("Explorer.Node.Program.cs").GuardedClick();
        session.WaitFor("EditorTab.Program.cs");
        var editor = session.WaitFor("TextEditor");

        editor.GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
        Keyboard.Type("// правка\r");
        session.SaveScreenshot("editor-dirty");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        Retry.WhileFalse(() => File.ReadAllText(_file).StartsWith("// правка", StringComparison.Ordinal), FileTimeout, throwOnTimeout: true);
        Assert.Contains("\r\n", File.ReadAllText(_file), StringComparison.Ordinal);

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        session.WaitUntilGone("EditorTab.Program.cs");
        session.WaitFor(AutomationIds.Welcome);
    }

    [Fact]
    public void StatusBarSearchAndThemedHighlighting()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
        session.WaitFor("TextEditor").GuardedClick();
        Assert.Single(session.MainWindow.FindAllDescendants(condition => condition.ByAutomationId("EditorTab.Program.cs")));

        Assert.Equal("C#", session.WaitFor("StatusBar.editor.language").Name);
        Assert.Equal("UTF-8", session.Find("StatusBar.editor.encoding").Name);
        Assert.Equal("CRLF", session.Find("StatusBar.editor.lineEnding").Name);

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        session.WaitForFocus("TextEditor.Search.Query");
        Keyboard.Type("Main");
        session.SaveScreenshot("editor-dark-search");
        Keyboard.Type(VirtualKeyShort.ESCAPE);

        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        session.WaitForText(AutomationIds.StatusBarMessage, text => text.Contains("светлая", StringComparison.Ordinal), FileTimeout);
        session.SaveScreenshot("editor-light");
    }

    [Fact]
    public void TabContextMenu_CloseOthers()
    {
        File.WriteAllText(Path.Combine(_folder, "Other.cs"), "class Other { }\r\n");
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
        session.WaitFor("Explorer.Node.Other.cs").GuardedDoubleClick();

        session.WaitFor("EditorTab.Program.cs").GuardedRightClick();
        session.WaitFor(AutomationIds.MenuItem("workbench.editor.closeOthers")).GuardedClick();

        session.WaitUntilGone("EditorTab.Other.cs");
        session.WaitFor("EditorTab.Program.cs");
    }
}
