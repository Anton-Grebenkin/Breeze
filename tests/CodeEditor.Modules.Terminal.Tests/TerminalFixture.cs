using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Modules.Terminal.Services.Commands;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// In-memory <c>C:\repo</c> with a solution and projects (including UI tests), a fake process runner, documents, and
/// build and test services.
/// </summary>
internal sealed class TerminalFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public TerminalFixture()
    {
        FileSystem = new FakeFileSystem()
            .AddDirectory(Root)
            .AddFile(PathOf("App.slnx"), "<Solution />")
            .AddFile(PathOf("src/App/App.csproj"), "<Project />")
            .AddFile(PathOf("src/App/A.cs"), "class A { }")
            .AddFile(PathOf("tests/App.Tests/App.Tests.csproj"), "<Project />")
            .AddFile(PathOf("tests/App.UI.Tests/App.UI.Tests.csproj"), "<Project />")
            .AddFile(PathOf("tests/App.Testing/App.Testing.csproj"), "<Project />");
        Workspace = new Workspace(FileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        Index = new FileIndex(Workspace, FileSystem, NullLogger<FileIndex>.Instance);
        Workspace.Open(Root);
        Target = new DotNetTarget(Workspace, FileSystem);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        SaveBeforeRun = new SaveBeforeRun(Documents, new InlineUiDispatcher(), NullLogger<SaveBeforeRun>.Instance);
        Build = new DotNetBuild(Runner, Target, Workspace, Output, SaveBeforeRun, TimeProvider.System, NullLogger<DotNetBuild>.Instance);
        Tests = new DotNetTests(Runner, Target, Workspace, Index, FileSystem, Output, SaveBeforeRun, NullLogger<DotNetTests>.Instance);
        Background = new BackgroundCommands(Runner, Workspace, TimeProvider.System, NullLogger<BackgroundCommands>.Instance);
    }

    public BackgroundCommands Background { get; }

    public FakeFileSystem FileSystem { get; }

    public Workspace Workspace { get; }

    public FileIndex Index { get; }

    public FakeProcessRunner Runner { get; } = new();

    public FakeOutputService Output { get; } = new();

    /// <summary>Long tool outputs (files in the agent data folder in the app).</summary>
    public FakeOutputStore Outputs { get; } = new();

    public DocumentService Documents { get; }

    public SaveBeforeRun SaveBeforeRun { get; }

    public DotNetTarget Target { get; }

    public DotNetBuild Build { get; }

    public DotNetTests Tests { get; }

    public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        Background.Dispose();
        Build.Dispose();
        Documents.Dispose();
        Index.Dispose();
        Workspace.Dispose();
    }
}
