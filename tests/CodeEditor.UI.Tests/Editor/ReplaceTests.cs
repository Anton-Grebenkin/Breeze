using System.Drawing;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// Replace in file: <c>Ctrl+H</c> takes the selected word as the query, Replace goes one by one, Replace All is a
/// single undo step.
/// </summary>
public sealed class ReplaceTests : IDisposable
{
    private const string Position = "StatusBar.editor.position";

    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(5);

    private readonly string _folder;
    private readonly string _file;

    public ReplaceTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        _file = Path.Combine(_folder, "Notes.txt");
        File.WriteAllText(_file, "alpha beta alpha\r\nalpha gamma\r\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void CtrlH_ReplaceOneThenAll_UndoIsSingleStep()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Notes.txt").GuardedDoubleClick();
        var editor = session.WaitFor("TextEditor");

        // A double click selects the first "alpha" (right of the line numbers), which becomes the query.
        var bounds = editor.BoundingRectangle;
        GuardedMouse.DoubleClick(new Point(bounds.Left + 45, bounds.Top + 8));
        session.WaitForText(Position, text => text.Contains("выделено 5", StringComparison.Ordinal), FileTimeout);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
        Assert.Equal("alpha", session.WaitForValue("TextEditor.Search.Query", value => value.Length > 0, FileTimeout));

        session.WaitFor("TextEditor.Search.Replace").GuardedClick();
        Keyboard.Type("omega");
        session.SaveScreenshot("editor-replace");

        // Enter in the replace box replaces the first match, which the search has selected.
        Keyboard.Type(VirtualKeyShort.ENTER);
        Save(session);
        WaitForFile(text => text.StartsWith("omega beta alpha", StringComparison.Ordinal));

        session.Find("TextEditor.Search.ReplaceAll").GuardedClick();
        Save(session);
        WaitForFile(text => text == "omega beta omega\r\nomega gamma\r\n");

        // One undo reverts all replacements.
        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
        Save(session);
        WaitForFile(text => text == "omega beta alpha\r\nalpha gamma\r\n");
    }

    private static void Save(AppSession session)
    {
        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
    }

    private void WaitForFile(Func<string, bool> predicate) =>
        Retry.WhileFalse(() => predicate(File.ReadAllText(_file)), FileTimeout, throwOnTimeout: true);
}
