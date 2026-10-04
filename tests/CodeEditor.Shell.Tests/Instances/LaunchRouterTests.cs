using CodeEditor.Shell.Instances;
using CodeEditor.Testing;

namespace CodeEditor.Shell.Tests.Instances;

/// <summary>Where a launch goes, as in VS Code: an open folder activates its window, a file goes to its window.</summary>
public sealed class LaunchRouterTests
{
    private static readonly string Repo = Path.GetFullPath(@"C:\repo");
    private static readonly string Other = Path.GetFullPath(@"C:\other");
    private static readonly string RepoFile = Path.Combine(Repo, "src", "a.cs");
    private static readonly string LooseFile = Path.GetFullPath(@"C:\notes\todo.txt");

    private static readonly WindowEntry RepoWindow = Window(1, Repo, minutesAgo: 10);
    private static readonly WindowEntry EmptyWindow = Window(2, folder: null, minutesAgo: 1);

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddFile(RepoFile, "class A {}")
        .AddFile(LooseFile, "todo")
        .AddDirectory(Other);

    [Fact]
    public void NoArguments_FirstWindow_RestoresLastSession() =>
        Assert.Equal(new LaunchPlan { RestoreLastSession = true }, Plan(new LaunchRequest(null, NewWindow: false)));

    [Fact]
    public void NoArguments_WithOtherWindows_OpensEmptyWindow() =>
        Assert.Equal(new LaunchPlan(), Plan(new LaunchRequest(null, NewWindow: false), RepoWindow));

    [Fact]
    public void NewWindowFlag_OpensEmptyWindowEvenWhenAlone() =>
        Assert.Equal(new LaunchPlan(), Plan(new LaunchRequest(null, NewWindow: true)));

    [Fact]
    public void Folder_OpenInAnotherWindow_GoesToThatWindow()
    {
        var plan = Plan(new LaunchRequest(Repo + @"\", NewWindow: true), EmptyWindow, RepoWindow);

        Assert.Equal(RepoWindow, plan.Target);
        Assert.Equal(Repo + @"\", plan.TargetPath);
    }

    [Fact]
    public void Folder_NotOpenElsewhere_OpensHere() =>
        Assert.Equal(new LaunchPlan { Folder = Other }, Plan(new LaunchRequest(Other, NewWindow: false), RepoWindow));

    [Fact]
    public void File_InsideAnOpenFolder_GoesToThatWindow()
    {
        var plan = Plan(new LaunchRequest(RepoFile, NewWindow: false), EmptyWindow, RepoWindow);

        Assert.Equal(RepoWindow, plan.Target);
        Assert.Equal(RepoFile, plan.TargetPath);
    }

    [Fact]
    public void File_OutsideOpenFolders_GoesToTheMostRecentWindow() =>
        Assert.Equal(EmptyWindow, Plan(new LaunchRequest(LooseFile, NewWindow: false), EmptyWindow, RepoWindow).Target);

    [Fact]
    public void File_WithNewWindowFlag_OpensHereWithoutFolder() =>
        Assert.Equal(new LaunchPlan { File = LooseFile }, Plan(new LaunchRequest(LooseFile, NewWindow: true), RepoWindow));

    // A folder whose name starts like the open one is not inside it.
    [Fact]
    public void File_InFolderWithSamePrefix_IsNotInside()
    {
        var sibling = Path.GetFullPath(@"C:\repository\b.cs");
        _fileSystem.AddFile(sibling, "class B {}");

        Assert.Null(Plan(new LaunchRequest(sibling, NewWindow: true), RepoWindow).Target);
    }

    [Fact]
    public void Here_DropsTheTargetAndKeepsThePath()
    {
        var plan = Plan(new LaunchRequest(RepoFile, NewWindow: false), RepoWindow);

        Assert.Equal(new LaunchPlan { File = RepoFile }, plan.Here());
    }

    private LaunchPlan Plan(LaunchRequest request, params WindowEntry[] others) => LaunchRouter.Plan(request, _fileSystem, others);

    private static WindowEntry Window(int processId, string? folder, int minutesAgo) =>
        new(processId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), folder, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddMinutes(-minutesAgo));
}
