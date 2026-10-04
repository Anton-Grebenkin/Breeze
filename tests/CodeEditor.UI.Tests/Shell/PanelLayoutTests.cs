using System.Drawing;
using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Shell;

/// <summary>
/// Layout on the real window (ADR 0031): <c>Ctrl+\</c> splits the editor so two tabs show side by side, <c>Ctrl+1</c>
/// and <c>Ctrl+2</c> switch groups; Output moves into the editor area via its tab's context menu and back to the panel
/// via the editor tab menu; the header button maximizes the panel and restores it; a tab dragged to the document edge
/// opens a new group, and dropping it on the neighbor's tab strip joins them.
/// </summary>
public sealed class PanelLayoutTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly string _folder;

    public PanelLayoutTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "project")).FullName;
        File.WriteAllText(Path.Combine(_folder, "Program.cs"), "class Program { }\r\n");
        File.WriteAllText(Path.Combine(_folder, "Other.cs"), "class Other { }\r\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void SplitEditor_AndMoveOutputIntoTheEditorAndBack()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
        session.WaitFor("Explorer.Node.Other.cs").GuardedDoubleClick();
        session.WaitFor("EditorTab.Other.cs").GuardedClick();

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_5);
        session.WaitFor("EditorGroup.2");
        Assert.NotNull(session.WaitFor("EditorGroup.1").FindFirstDescendant(condition => condition.ByAutomationId("EditorTab.Program.cs")));
        Assert.NotNull(session.Find("EditorGroup.2").FindFirstDescendant(condition => condition.ByAutomationId("EditorTab.Other.cs")));
        session.SaveScreenshot("layout-split");

        session.Find(AutomationIds.MenuItem("menubar.view")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("workbench.view.output")).GuardedClick();
        session.WaitFor("Panel.Tab.output").GuardedRightClick();
        session.WaitFor("ToolWindow.output.MoveTo.Editor").GuardedClick();
        session.WaitFor("EditorTab.Вывод");
        session.SaveScreenshot("layout-output-in-editor");

        session.Find("EditorTab.Вывод").GuardedRightClick();
        session.WaitFor(AutomationIds.MenuItem("workbench.action.moveEditorView.Panel")).GuardedClick();
        session.WaitUntilGone("EditorTab.Вывод");
        session.WaitFor("Panel.Tab.output");

        // A maximized panel takes the documents' height; a second click restores its size.
        var editorHeight = session.Find("EditorGroup.1").BoundingRectangle.Height;
        session.Find("Panel.Maximize").GuardedClick();
        Assert.True(Retry.WhileFalse(() => session.Find("EditorGroup.1").BoundingRectangle.Height < editorHeight / 4, Timeout).Result, "панель не развернулась");
        session.SaveScreenshot("layout-panel-maximized");
        session.Find("Panel.Maximize").GuardedClick();
        Assert.True(Retry.WhileFalse(() => session.Find("EditorGroup.1").BoundingRectangle.Height == editorHeight, Timeout).Result, "размер панели не вернулся");
    }

    [Fact]
    public void DragTab_ToTheEdgeOpensAGroup_AndOntoTheNeighbourStripJoins()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.Program.cs").GuardedDoubleClick();
        session.WaitFor("Explorer.Node.Other.cs").GuardedDoubleClick();

        var group = session.WaitFor("EditorGroup.1").BoundingRectangle;
        session.WaitFor("EditorTab.Other.cs").GuardedDrag(new Point(group.Right - group.Width / 10, group.Top + group.Height / 2));
        Assert.NotNull(session.WaitFor("EditorGroup.2").FindFirstDescendant(condition => condition.ByAutomationId("EditorTab.Other.cs")));
        session.SaveScreenshot("layout-drag-split");

        // Dropping on the empty right part of the tab strip appends to the first group.
        var strip = session.Find("EditorGroup.1.Tabs").BoundingRectangle;
        group = session.Find("EditorGroup.1").BoundingRectangle;
        session.Find("EditorTab.Other.cs").GuardedDrag(new Point(group.Right - 24, strip.Top + strip.Height / 2));
        session.WaitUntilGone("EditorGroup.2");
        var tabs = session.Find("EditorGroup.1.Tabs").FindAllDescendants()
            .Select(element => element.Properties.AutomationId.ValueOrDefault ?? string.Empty)
            .Where(id => id.StartsWith("EditorTab.", StringComparison.Ordinal) && !id.EndsWith(".Close", StringComparison.Ordinal));
        Assert.Equal(["EditorTab.Program.cs", "EditorTab.Other.cs"], tabs);
    }
}
