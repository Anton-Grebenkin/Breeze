using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary>Reverting agent changes: original text to the tab, created file to the bin, deleted file restored.</summary>
public sealed class AgentChangeReverterTests : IDisposable
{
    private const string Original = "class A { }\n";

    private readonly EditorFixture _fixture = new();
    private readonly AgentChangeReverter _reverter;
    private readonly string _path = EditorFixture.PathOf("src/A.cs");

    public AgentChangeReverterTests() => _reverter = new AgentChangeReverter(_fixture.Editors, _fixture.FileSystem, _fixture.Dispatcher, _fixture.Documents);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task EditedFile_GetsOriginalText_AsOneUndoableStep()
    {
        _fixture.FileSystem.AddFile(_path, Original);
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(_path));
        tab!.Document.Buffer.Replace(0, 0, "// агент\n");

        await _reverter.RevertAsync(_path, Original);

        Assert.Equal(Original, tab.Document.Buffer.GetText());
        tab.Document.Buffer.Undo();
        Assert.Equal("// агент\n" + Original, tab.Document.Buffer.GetText());
    }

    // Agent edits are on disk (ADR 0040), so the revert is saved too; unsaved typing of the user would stay unsaved.
    [Fact]
    public async Task SavedAgentEdit_RevertIsSaved()
    {
        _fixture.FileSystem.AddFile(_path, Original);
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(_path));
        tab!.Document.Buffer.Replace(0, 0, "// агент\n");
        await _fixture.Documents.SaveAsync(tab.Document, TestContext.Current.CancellationToken);

        await _reverter.RevertAsync(_path, Original);

        Assert.False(tab.Document.IsDirty);
        Assert.Equal(Original, _fixture.FileSystem.ReadAllText(_path));
    }

    [Fact]
    public async Task CreatedFile_TabClosesWithoutQuestion_FileGoesToRecycleBin()
    {
        _fixture.FileSystem.AddFile(_path, Original);
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(_path));
        tab!.Document.Buffer.Replace(0, 0, "// ещё правка\n");

        await _reverter.RevertAsync(_path, originalText: null);

        Assert.Empty(_fixture.Editors.Tabs);
        Assert.False(_fixture.FileSystem.FileExists(_path));
        Assert.Contains(_path, _fixture.FileSystem.RecycledPaths);
    }

    [Fact]
    public async Task DeletedFile_IsWrittenBack_AndOpened()
    {
        await _reverter.RevertAsync(_path, Original);

        Assert.Equal(Original, _fixture.FileSystem.ReadAllText(_path));
        Assert.Equal(_path, Assert.Single(_fixture.Editors.Tabs).FilePath);
    }
}
