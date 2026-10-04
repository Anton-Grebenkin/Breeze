using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Services;
using CodeEditor.Shell.Editors;
using CodeEditor.Shell.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary>In-memory <c>C:\repo</c>, documents and text editor tabs; agent changes use <see cref="FileState"/>.</summary>
internal sealed class EditorFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\repo");

    public EditorFixture()
    {
        var context = new ContextKeyService();
        Workspace = new Workspace(FileSystem, context, NullLogger<Workspace>.Instance);
        Workspace.Open(Root);
        Documents = new DocumentService(FileSystem, new TestTextBufferFactory(), Dispatcher, Workspace, NullLogger<DocumentService>.Instance);
        var statusBar = new StatusBarViewModel();
        var provider = new TextEditorProvider(new EditorSettings(new TestOptionsMonitor<EditorOptions>(new EditorOptions())), new EditorFocus(context), FileState, Documents, new Lazy<IAgentChangeReverter>(Reverter), Dispatcher, Time);
        Editors = new EditorAreaViewModel(Documents, [provider], [], new DocumentSaver(Documents, new FakeDialogs(), statusBar), context, statusBar);
    }

    public FakeFileSystem FileSystem { get; } = new FakeFileSystem().AddDirectory(Path.GetFullPath(@"C:\repo"));

    public InlineUiDispatcher Dispatcher { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public AgentFileState FileState { get; } = new();

    public FakeReverter Reverter { get; } = new();

    public Workspace Workspace { get; }

    public DocumentService Documents { get; }

    public EditorAreaViewModel Editors { get; }

    public static string PathOf(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        Documents.Dispose();
        Workspace.Dispose();
    }

    /// <summary>Records revert requests instead of touching tabs and files.</summary>
    public sealed class FakeReverter : IAgentChangeReverter
    {
        public List<(string Path, string? Original)> Reverted { get; } = [];

        public Task RevertAsync(string path, string? originalText)
        {
            Reverted.Add((path, originalText));
            return Task.CompletedTask;
        }
    }
}
