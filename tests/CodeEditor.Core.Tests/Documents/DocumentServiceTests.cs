using System.Text;
using CodeEditor.Core.Context;
using CodeEditor.Core.Documents;
using CodeEditor.Core.Files;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Core.Tests.Documents;

public sealed class DocumentServiceTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");
    private static readonly string Program = Path.Combine(Root, "Program.cs");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddFile(Program, "class Program {}\n");
    private readonly Workspace _workspace;
    private readonly DocumentService _documents;

    public DocumentServiceTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(Root);
        _documents = new DocumentService(_fileSystem, new TestTextBufferFactory(), new InlineUiDispatcher(), _workspace, NullLogger<DocumentService>.Instance);
    }

    public void Dispose()
    {
        _documents.Dispose();
        _workspace.Dispose();
    }

    [Fact]
    public async Task Open_ReadsTextAndFormat_OnlyOnce()
    {
        var opened = 0;
        _documents.Opened += (_, _) => opened++;

        var first = await Open();
        var second = await Open();

        Assert.Same(first, second);
        Assert.Equal("class Program {}\n", first.Buffer.GetText());
        Assert.Equal(LineEnding.Lf, first.Format.LineEnding);
        Assert.Equal("Program.cs", first.Name);
        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task Open_SameFileConcurrently_GivesOneDocument()
    {
        var documents = await Task.WhenAll(Open(), Open());

        Assert.Same(documents[0], documents[1]);
        Assert.Single(_documents.Documents);
    }

    [Fact]
    public async Task Open_BinaryFile_Throws()
    {
        var path = Path.Combine(Root, "app.dll");
        _fileSystem.AddBytes(path, [0x4D, 0x5A, 0x00, 0x00]);

        var error = await Assert.ThrowsAsync<DocumentOpenException>(() => _documents.OpenAsync(path, TestContext.Current.CancellationToken));

        Assert.Contains("двоичный", error.Message, StringComparison.Ordinal);
        Assert.Empty(_documents.Documents);
    }

    [Fact]
    public async Task Edit_MakesDirty_SaveWritesAndClears()
    {
        var document = await Open();
        var states = 0;
        document.StateChanged += (_, _) => states++;

        document.Buffer.Replace(0, 5, "struct");
        Assert.True(document.IsDirty);

        await _documents.SaveAsync(document, TestContext.Current.CancellationToken);

        Assert.False(document.IsDirty);
        Assert.Equal("struct Program {}\n", Encoding.UTF8.GetString(_fileSystem.ReadAllBytes(Program)));
        Assert.Equal(2, states);
    }

    [Fact]
    public async Task UndoToSavedState_IsNotDirty()
    {
        var document = await Open();

        document.Buffer.Replace(0, 0, "// ");
        document.Buffer.Undo();

        Assert.False(document.IsDirty);
    }

    [Fact]
    public async Task ExternalChange_WithoutEdits_ReloadsSilently()
    {
        var document = await Open();
        _fileSystem.AddFile(Program, "class Changed {}\n");
        _fileSystem.Touch(Program);

        _fileSystem.Watchers[0].Raise(new FileChange(Program, FileChangeKind.Changed));
        await WaitUntil(() => document.Buffer.GetText().Contains("Changed", StringComparison.Ordinal));

        Assert.False(document.IsDirty);
        Assert.False(document.HasExternalChanges);
    }

    [Fact]
    public async Task ExternalChange_WithEdits_MarksConflict()
    {
        var document = await Open();
        document.Buffer.Replace(0, 0, "// мои правки\n");
        _fileSystem.Touch(Program);

        _fileSystem.Watchers[0].Raise(new FileChange(Program, FileChangeKind.Changed));

        Assert.True(document.HasExternalChanges);
        Assert.StartsWith("// мои правки", document.Buffer.GetText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OwnSave_IsNotAnExternalChange()
    {
        var document = await Open();
        document.Buffer.Replace(0, 0, "// ");
        await _documents.SaveAsync(document, TestContext.Current.CancellationToken);
        document.Buffer.Replace(0, 0, "x");

        _fileSystem.Watchers[0].Raise(new FileChange(Program, FileChangeKind.Changed));

        Assert.False(document.HasExternalChanges);
    }

    [Fact]
    public async Task DeletedOnDisk_IsFlagged()
    {
        var document = await Open();
        _fileSystem.DeleteToRecycleBin(Program);

        _fileSystem.Watchers[0].Raise(new FileChange(Program, FileChangeKind.Deleted));

        Assert.True(document.IsDeletedOnDisk);
    }

    [Fact]
    public async Task SaveAll_SavesOnlyDirty()
    {
        var other = Path.Combine(Root, "Other.cs");
        _fileSystem.AddFile(other, "a");
        var program = await Open();
        var clean = await _documents.OpenAsync(other, TestContext.Current.CancellationToken);
        program.Buffer.Replace(0, 0, "// ");
        var saved = new List<string>();
        _documents.Saved += (_, document) => saved.Add(document.Name);

        await _documents.SaveAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Program.cs"], saved);
        Assert.False(clean.IsDirty);
    }

    [Fact]
    public async Task Close_ForgetsDocument()
    {
        var document = await Open();

        _documents.Close(document);

        Assert.Empty(_documents.Documents);
        Assert.False(_documents.TryGet(Program, out _));
    }

    private Task<IDocument> Open() => _documents.OpenAsync(Program, TestContext.Current.CancellationToken);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }
}
