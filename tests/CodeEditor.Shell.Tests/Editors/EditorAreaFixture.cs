using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Shell.Tests.Editors;

/// <summary>
/// Editor area over an in-memory file system: <c>C:\repo\a.cs</c>, <c>b.cs</c>, <c>c.cs</c>, <c>app.dll</c>.
/// </summary>
internal sealed class EditorAreaFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public EditorAreaFixture(params IFileViewerProvider[] viewers)
    {
        FileSystem = new FakeFileSystem()
            .AddFile(PathOf("a.cs"), "class A {}")
            .AddFile(PathOf("b.cs"), "class B {}")
            .AddFile(PathOf("c.cs"), "class C {}")
            .AddBytes(PathOf("app.dll"), [0x4D, 0x5A, 0x00]);

        Workspace = new Core.Files.Workspace(FileSystem, Context, NullLogger<Core.Files.Workspace>.Instance);
        Workspace.Open(Root);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), Workspace, NullLogger<DocumentService>.Instance);
        Area = new EditorAreaViewModel(Documents, [new PlainEditorProvider()], viewers, new DocumentSaver(Documents, Dialogs, StatusBar), Context, StatusBar);
    }

    public FakeFileSystem FileSystem { get; }

    public ContextKeyService Context { get; } = new();

    public Core.Files.Workspace Workspace { get; }

    public DocumentService Documents { get; }

    public FakeDialogs Dialogs { get; } = new();

    public StatusBarViewModel StatusBar { get; } = new();

    public EditorAreaViewModel Area { get; }

    public static string PathOf(string name) => Path.Combine(Root, name);

    public async Task<EditorTabViewModel> OpenAsync(string name, bool preview = false) =>
        (await Area.OpenTextAsync(new OpenFileRequest(PathOf(name), preview)))!;

    public IEnumerable<string> TabNames => Area.Tabs.Select(tab => tab.Title);

    public void Dispose()
    {
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
