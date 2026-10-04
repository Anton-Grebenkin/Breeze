using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Modules.TextEditor.ViewModels;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.TextEditor.Tests;

public sealed class EditorAgentToolsTests : IDisposable
{
    private readonly EditorFixture _fixture = new();
    private readonly Dictionary<string, AIFunction> _tools;

    public EditorAgentToolsTests()
    {
        _fixture.FileSystem
            .AddFile(EditorFixture.PathOf("src/a.cs"), "class A\n{\n    void Run() { }\n}\n")
            .AddFile(EditorFixture.PathOf("b.md"), "# B");
        _tools = new EditorAgentTools(_fixture.Editors, _fixture.Workspace, _fixture.Dispatcher).CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task OpenDocuments_MarkUnsavedAndActive()
    {
        Assert.Equal("(нет открытых файлов)", await Invoke("get_open_documents"));

        var a = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("src/a.cs")));
        await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("b.md")));
        a!.Document.Buffer.Replace(0, 0, "// ");

        Assert.Equal("src/a.cs (unsaved)\nb.md (active)", await Invoke("get_open_documents"));
    }

    [Fact]
    public async Task Selection_ReturnsFileCaretAndSelectedText()
    {
        var tab = await _fixture.Editors.OpenTextAsync(new OpenFileRequest(EditorFixture.PathOf("src/a.cs")));
        var editor = (TextEditorViewModel)tab!.Editor;
        editor.CaretLine = 3;
        editor.CaretColumn = 14;
        editor.SelectionStart = 19;
        editor.SelectionLength = 3;

        Assert.Equal("file: src/a.cs\ncaret: line 3, column 14\nselection (3 chars):\nRun", await Invoke("get_selection"));
    }

    private async Task<string?> Invoke(string tool) =>
        (await _tools[tool].InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken))?.ToString();
}
