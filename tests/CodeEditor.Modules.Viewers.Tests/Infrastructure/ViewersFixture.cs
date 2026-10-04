using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Viewers.ViewModels;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Viewers.Tests.Infrastructure;

/// <summary>
/// In-memory <c>C:\work</c> folder with viewer services on top: a fake decoder, in-memory bytes, time and UI thread
/// controlled by the test; executed commands and files opened externally are recorded.
/// </summary>
internal sealed class ViewersFixture : IDisposable
{
    public static readonly string Root = Path.GetFullPath(@"C:\work");

    public ViewersFixture(IUiDispatcher? dispatcher = null)
    {
        Dispatcher = dispatcher ?? new InlineUiDispatcher();
        Workspace = new Workspace(Files, new ContextKeyService(), NullLogger<Workspace>.Instance);
        Workspace.Open(Root);
        Context = new ViewerContext(Files, Bytes, Decoder, Workspace, Shell, Dispatcher, Time, Commands);
    }

    public FakeFileSystem Files { get; } = new FakeFileSystem().AddDirectory(Root);

    public MemoryFileBytes Bytes { get; } = new();

    public FakeImageDecoder Decoder { get; } = new();

    public FakeSystemShell Shell { get; } = new();

    public RecordingCommands Commands { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public IUiDispatcher Dispatcher { get; }

    public Workspace Workspace { get; }

    public ViewerContext Context { get; }

    public static string PathOf(string name) => Path.Combine(Root, name);

    /// <summary>The file changed on disk: a workspace event, as from the file watcher.</summary>
    public void ReportChange(string path) =>
        Files.Watchers.Last().Raise(new FileChange(path, FileChangeKind.Changed));

    public void Dispose() => Workspace.Dispose();
}
