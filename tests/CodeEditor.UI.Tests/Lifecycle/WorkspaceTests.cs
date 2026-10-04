using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Lifecycle;

/// <summary>
/// Folder from the command line: its name in the title and search box, the folder in File → Open Recent.
/// </summary>
public sealed class WorkspaceTests : IDisposable
{
    private const string FolderName = "sample-project";

    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), FolderName)).FullName;

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void FolderArgument_OpensWorkspace()
    {
        using var session = AppSession.WithArguments(_folder);

        Assert.StartsWith(FolderName, session.MainWindow.Title, StringComparison.Ordinal);
        Assert.Equal(FolderName, session.Find(AutomationIds.TitleBarSearch).Name);
        session.SaveScreenshot("workspace-open");

        session.Find(AutomationIds.MenuItem("menubar.file")).GuardedClick();
        session.WaitFor(AutomationIds.MenuItem("menubar.file.recent")).GuardedClick();
        var recent = session.WaitFor(AutomationIds.MenuItem("workbench.folder.openRecent.0"));
        Assert.Equal(_folder, recent.Name.Replace("__", "_", StringComparison.Ordinal));

        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
    }
}
