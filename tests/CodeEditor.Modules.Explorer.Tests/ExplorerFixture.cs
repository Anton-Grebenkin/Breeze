using CodeEditor.Core.Commands;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Explorer.Tests;

/// <summary>
/// Explorer over an in-memory file system:
/// <code>
/// C:\repo\
///   .gitignore (*.log)
///   docs\guide.md
///   src\Program.cs, src\file10.cs, src\file2.cs, src\bin\
///   README.md, debug.log
/// </code>
/// </summary>
internal sealed class ExplorerFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public ExplorerFixture()
    {
        FileSystem = new FakeFileSystem()
            .AddFile(Path.Combine(Root, ".gitignore"), "*.log")
            .AddFile(Path.Combine(Root, "docs", "guide.md"))
            .AddFile(Path.Combine(Root, "src", "Program.cs"))
            .AddFile(Path.Combine(Root, "src", "file10.cs"))
            .AddFile(Path.Combine(Root, "src", "file2.cs"))
            .AddDirectory(Path.Combine(Root, "src", "bin"))
            .AddFile(Path.Combine(Root, "README.md"))
            .AddFile(Path.Combine(Root, "debug.log"));

        Workspace = new Workspace(FileSystem, Context, NullLogger<Workspace>.Instance);
        CommandService = new CommandService(Commands, Context, NullLogger<CommandService>.Instance);
        Commands.Register(new CommandDefinition("workbench.file.open", "Открыть файл", (argument, _) =>
        {
            var request = (OpenFileRequest)argument!;
            OpenedFiles.Add(request.FilePath);
            PreviewFlags.Add(request.Preview);
            return ValueTask.CompletedTask;
        }));

        Explorer = new ExplorerViewModel(Workspace, FileSystem, CommandService, Context, new InlineUiDispatcher());
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        Editors = new EditorAreaViewModel(Documents, [new PlainEditorProvider()], [], new DocumentSaver(Documents, Dialogs, StatusBar), Context, StatusBar);
        var tabs = new EditorTabRelocator(Editors);
        Editor = new ExplorerEditor(Explorer, FileSystem, Workspace, Dialogs, SystemShell, CommandService, StatusBar, tabs);
        Drop = new ExplorerDrop(Explorer, FileSystem, Dialogs, StatusBar, tabs);
    }

    public FakeFileSystem FileSystem { get; }

    public ContextKeyService Context { get; } = new();

    public CommandRegistry Commands { get; } = new();

    public CommandService CommandService { get; }

    public Workspace Workspace { get; }

    public StatusBarViewModel StatusBar { get; } = new();

    public FakeDialogs Dialogs { get; } = new();

    public FakeSystemShell SystemShell { get; } = new();

    public ExplorerViewModel Explorer { get; }

    public ExplorerEditor Editor { get; }

    public ExplorerDrop Drop { get; }

    /// <summary>Editor area for checking that open tabs follow moved files.</summary>
    public DocumentService Documents { get; }

    public EditorAreaViewModel Editors { get; }

    public List<string> OpenedFiles { get; } = [];

    public List<bool> PreviewFlags { get; } = [];

    public FileTree Tree => Explorer.Tree!;

    public async Task OpenAsync()
    {
        Workspace.Open(Root);
        await Explorer.RootLoaded;
    }

    public async Task<FileNodeViewModel> ExpandAsync(string relativePath)
    {
        Assert.True(Tree.TryGet(Path.Combine(Root, relativePath), out var node));
        await Tree.LoadChildrenAsync(node);
        node.IsExpanded = true;
        return node;
    }

    public FileNodeViewModel Node(string relativePath)
    {
        Assert.True(Tree.TryGet(Path.Combine(Root, relativePath), out var node), $"Нет узла {relativePath}");
        return node;
    }

    public void Dispose()
    {
        Explorer.Dispose();
        Documents.Dispose();
        Workspace.Dispose();
    }

    private sealed class PlainEditorProvider : IEditorProvider
    {
        public int Priority => 0;

        public bool CanOpen(string filePath) => true;

        public object CreateEditor(IDocument document) => document;
    }
}
