using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Search in files: <c>Ctrl+Shift+F</c> focuses the box, results are grouped by file, a click opens the match
/// selected, <c>Alt+C</c> toggles match case.
/// </summary>
public sealed class SearchTests : IDisposable
{
    private const string Position = "StatusBar.editor.position";

    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromSeconds(5);

    private readonly string _folder;

    public SearchTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        var utils = Directory.CreateDirectory(Path.Combine(_folder, "src", "Utils")).FullName;
        File.WriteAllText(Path.Combine(_folder, "src", "Program.cs"), "class Program\r\n{\r\n    static void Main() => Helper.Run();\r\n}\r\n");
        File.WriteAllText(Path.Combine(utils, "Helper.cs"), "static class Helper\r\n{\r\n    public static void Run() { }\r\n}\r\n");
        File.WriteAllText(Path.Combine(_folder, "README.md"), "# Проект\r\nhelper — вспомогательный класс\r\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void CtrlShiftF_SearchesAndOpensMatch()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.src");
        session.FocusWindow();

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
        session.WaitForFocus("Search.Query");
        Keyboard.Type("Helper");

        session.WaitForText("Search.Summary", text => text.StartsWith("3 результата в 3 файлах", StringComparison.Ordinal), UpdateTimeout);
        session.SaveScreenshot("search-results");

        // Match case: Alt+C keeps only capitalized "Helper", so README drops out.
        Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_C);
        session.WaitForText("Search.Summary", text => text.StartsWith("2 результата в 2 файлах", StringComparison.Ordinal), UpdateTimeout);

        session.WaitFor("Search.Match.src/Program.cs:3:27").GuardedDoubleClick();
        session.WaitFor("EditorTab.Program.cs");
        session.WaitForText(Position, text => text == "Стр. 3, стлб. 33 (выделено 6)", UpdateTimeout);
        session.SaveScreenshot("search-opened");
    }

    [Fact]
    public void EditMenu_OpensSearchWithMouse()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.src");

        session.Find(AutomationIds.MenuItem("menubar.edit")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("workbench.view.search")).GuardedClick();

        session.WaitForFocus("Search.Query");
        session.Find("Search.ToggleFilters").GuardedClick();
        session.WaitFor("Search.Include");
    }
}
