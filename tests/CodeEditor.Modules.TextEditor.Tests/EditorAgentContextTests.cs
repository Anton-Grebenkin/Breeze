using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class EditorAgentContextTests : IDisposable
{
    private readonly EditorFixture _fixture = new();
    private readonly EditorAgentContext _context;

    public EditorAgentContextTests()
    {
        _fixture.FileSystem.AddFile(EditorFixture.PathOf("src/a.cs"), "class A\n{\n    void Run() { }\n}\n");
        _context = new EditorAgentContext(_fixture.Editors, _fixture.Workspace, _fixture.Dispatcher);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task NoOpenFile_GivesNothing()
    {
        Assert.Empty(await Get(includeEditor: true));
    }

    [Fact]
    public async Task OpenFile_GivesPathCaretAndSelectedLines_WithoutText()
    {
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("src/a.cs")));
        var editor = (TextEditorViewModel)tab!.Editor;
        editor.CaretLine = 3;
        Assert.Equal(["Открыт файл src/a.cs, курсор в строке 3."], await Get(includeEditor: true));

        editor.SelectionLines = (2, 4);
        tab.Document.Buffer.Replace(0, 0, "// ");

        var line = Assert.Single(await Get(includeEditor: true));
        Assert.Equal("Открыт файл src/a.cs (есть несохранённые изменения), курсор в строке 3; выделены строки 2–4.", line);
        Assert.DoesNotContain("Run", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetachedFileChip_GivesNothing()
    {
        await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("src/a.cs")));

        Assert.Empty(await Get(includeEditor: false));
    }

    private async Task<IReadOnlyList<string>> Get(bool includeEditor) =>
        await _context.GetContextAsync(new AgentContextRequest(includeEditor), TestContext.Current.CancellationToken);
}
