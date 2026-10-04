using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Diagrams.Services;
using CodeEditor.Modules.Diagrams.Services.Export;
using CodeEditor.Modules.Diagrams.ViewModels;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Diagrams.Tests.Infrastructure;

/// <summary>
/// In-memory <c>C:\repo</c> folder, documents, the tab area and diagram services on a fake renderer. The test controls
/// time: re-rendering after an edit waits for <see cref="DiagramPreviewViewModel.TypingDelay"/>.
/// </summary>
internal sealed class DiagramsFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public DiagramsFixture()
    {
        FileSystem.AddDirectory(Root);
        Workspace = new Workspace(FileSystem, Context, NullLogger<Workspace>.Instance);
        Workspace.Open(Root);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), Dispatcher, Workspace, NullLogger<DocumentService>.Instance);
        Editors = new EditorAreaViewModel(Documents, [new PlainEditorProvider()], [], new DocumentSaver(Documents, new FakeDialogs(), StatusBar), Context, StatusBar);
        Reader = new DiagramTextReader(Documents, FileSystem, Dispatcher);
        Exporter = new DiagramExporter(Renderer, FileSystem, Writes);
        Exports = new DiagramExports(Exporter, StatusBar);
        Services = new DiagramPreviewServices(Renderer, Documents, Reader, Workspace, Dispatcher, Themes, Time, Exports, Commands);
    }

    public FakeFileSystem FileSystem { get; } = new();

    public ContextKeyService Context { get; } = new();

    public InlineUiDispatcher Dispatcher { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public FakeDiagramRenderer Renderer { get; } = new();

    public FakeThemeService Themes { get; } = new();

    public FakeCommandService Commands { get; } = new();

    public StatusBarViewModel StatusBar { get; } = new();

    public DiagramWrites Writes { get; } = new();

    public Workspace Workspace { get; }

    public DocumentService Documents { get; }

    public EditorAreaViewModel Editors { get; }

    public DiagramTextReader Reader { get; }

    public DiagramExporter Exporter { get; }

    public DiagramExports Exports { get; }

    public DiagramPreviewServices Services { get; }

    public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public DiagramsFixture AddFile(string relative, string text)
    {
        FileSystem.AddFile(PathOf(relative), text);
        return this;
    }

    /// <summary>Opens a file in a tab like a user would: a document whose buffer the test edits.</summary>
    public async Task<IDocument> OpenAsync(string relative) =>
        (await Editors.OpenTextAsync(new OpenFileRequest(PathOf(relative))))!.Document;

    /// <summary>Preview of a file; the first render has no delay.</summary>
    public async Task<DiagramPreviewViewModel> PreviewAsync(string relative)
    {
        var preview = new DiagramPreviewViewModel(PathOf(relative), Services);
        await preview.Pending;
        return preview;
    }

    /// <summary>Lets the typing pause elapse, then starts the re-render and waits for it.</summary>
    public async Task AfterTypingAsync(DiagramPreviewViewModel preview)
    {
        Time.Advance(DiagramPreviewViewModel.TypingDelay);
        await preview.Pending;
    }

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
