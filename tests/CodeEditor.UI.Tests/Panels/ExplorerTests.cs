using CodeEditor.UI.Tests.Infrastructure;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;

namespace CodeEditor.UI.Tests.Panels;

/// <summary>
/// Explorer on a real folder: shown on open, expands folders, creates a file inline in the tree, renames with F2.
/// </summary>
public sealed class ExplorerTests : IDisposable
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(5);

    private readonly string _folder;

    public ExplorerTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "demo")).FullName;
        Directory.CreateDirectory(Path.Combine(_folder, "src"));
        Directory.CreateDirectory(Path.Combine(_folder, "bin"));
        File.WriteAllText(Path.Combine(_folder, "src", "Program.cs"), "class Program {}");
        File.WriteAllText(Path.Combine(_folder, "README.md"), "# demo");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void Explorer_ShowsTree_CreatesAndRenamesFiles()
    {
        using var session = AppSession.WithArguments(_folder);

        session.WaitFor(Node("README.md"));
        Assert.Null(session.TryFind(Node("bin")));

        session.Find(Node("src")).GuardedClick();
        Keyboard.Type(VirtualKeyShort.RIGHT);
        session.WaitFor(Node("Program.cs"));
        session.SaveScreenshot("explorer-tree");

        // New file in the selected folder: header button, name, Enter.
        session.Find("Explorer.NewFile").GuardedClick();
        session.WaitForFocus("Explorer.EditName");
        Keyboard.Type("Service.cs");
        Keyboard.Type(VirtualKeyShort.ENTER);
        var created = Path.Combine(_folder, "src", "Service.cs");
        Retry.WhileFalse(() => File.Exists(created), FileTimeout, throwOnTimeout: true);
        session.WaitFor(Node("Service.cs"));

        // Rename with F2: the name without extension is preselected.
        session.Find(Node("Service.cs")).GuardedClick();
        Keyboard.Type(VirtualKeyShort.F2);
        session.WaitForFocus("Explorer.EditName");
        Keyboard.Type("Worker");
        Keyboard.Type(VirtualKeyShort.ENTER);
        Retry.WhileFalse(() => File.Exists(Path.Combine(_folder, "src", "Worker.cs")), FileTimeout, throwOnTimeout: true);
        session.WaitFor(Node("Worker.cs"));
    }

    private static string Node(string name) => $"Explorer.Node.{name}";
}
