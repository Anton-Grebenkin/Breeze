using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// Diagrams with the bundled Mermaid (ADR 0035): <c>Ctrl+K V</c> opens the preview in the second group beside the text
/// (ADR 0031), the chord hint leaves the status bar, and the diagram renders offline: its node labels appear in the
/// window's accessibility tree.
/// </summary>
public sealed class DiagramPreviewTests : IDisposable
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(30);

    private readonly string _folder;

    public DiagramPreviewTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "docs")).FullName;
        File.WriteAllText(Path.Combine(_folder, "arch.mmd"), "flowchart LR\n  A[Заказ] --> B[Оплата]\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void PreviewToTheSide_DrawsTheDiagram()
    {
        using var session = AppSession.WithArguments(_folder);
        session.WaitFor("Explorer.Node.arch.mmd").GuardedDoubleClick();
        session.WaitFor("EditorTab.arch.mmd");
        session.WaitForText("StatusBar.editor.language", text => text == "Mermaid", RenderTimeout);

        session.WaitFor("TextEditor").GuardedClick();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_K);
        Keyboard.Type(VirtualKeyShort.KEY_V);

        var preview = session.WaitFor("EditorGroup.2").FindFirstDescendant(condition => condition.ByAutomationId("EditorTab.Предпросмотр arch.mmd"));
        Assert.NotNull(preview);
        session.WaitForText(AutomationIds.StatusBarMessage, text => !text.Contains("Ожидание", StringComparison.Ordinal), RenderTimeout);
        Assert.True(Retry.WhileNull(() => session.MainWindow.FindFirstDescendant(condition => condition.ByName("Оплата")), RenderTimeout).Success,
            "Схема не нарисовалась: подписи узла нет в окне.");
        session.SaveScreenshot("diagram-preview");
    }
}
