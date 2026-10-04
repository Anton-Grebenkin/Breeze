using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.TextEditor.Services.Agent;
using CodeEditor.Shell.Editors;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary><c>apply_patch</c> (V4A) produces the same edits as <c>apply_edits</c>: tabs, CRLF, preview, new files.</summary>
public sealed class ApplyPatchTests : IDisposable
{
    private const string Twice = "class A\r\n{\r\n    void Run() { }\r\n}\r\nclass B\r\n{\r\n    void Run() { }\r\n}\r\n";

    private readonly EditorFixture _fixture = new();
    private readonly AgentFileState _fileState = new();
    private readonly EditingAgentTools _tools;
    private readonly AIFunction _patch;

    public ApplyPatchTests()
    {
        _fixture.FileSystem.AddFile(EditorFixture.PathOf("src/A.cs"), Twice);
        var path = EditorFixture.PathOf("src/A.cs");
        _fileState.RecordRead(path, Twice, 1, 1);
        var planner = new EditPlanner(_fixture.Workspace, _fixture.FileSystem, _fixture.Documents, _fixture.Dispatcher, _fileState);
        _tools = new EditingAgentTools(planner, _fixture.Editors, _fixture.Workspace, _fixture.FileSystem, _fixture.Dispatcher, _fileState, _fixture.Documents);
        _patch = _tools.CreateTools().OfType<AIFunction>().Single(tool => tool.Name == EditingAgentTools.ApplyPatchName);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Anchor_PicksTheRightOccurrence_CrlfKept()
    {
        var result = await Invoke("*** Begin Patch\n*** Update File: src/A.cs\n@@ class B\n {\n-    void Run() { }\n+    void Go() { }\n }\n*** End Patch");

        Assert.StartsWith("Изменены файлы: src/A.cs", result, StringComparison.Ordinal);
        Assert.Equal(Twice.Replace("B\r\n{\r\n    void Run()", "B\r\n{\r\n    void Go()", StringComparison.Ordinal), Tab("A.cs").Document.Buffer.GetText());
    }

    [Fact]
    public async Task WithoutAnchor_AmbiguousContext_IsAnError()
    {
        var error = await Assert.ThrowsAsync<AgentToolException>(() => Invoke("*** Begin Patch\n*** Update File: src/A.cs\n {\n-    void Run() { }\n+    void Go() { }\n*** End Patch"));

        Assert.Contains("несколько раз", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFile_CreatesAndOpensIt_AndPreviewShowsBoth()
    {
        const string input = "*** Begin Patch\n*** Update File: src/A.cs\n@@ class A\n {\n-    void Run() { }\n+    void Start() { }\n*** Add File: src/C.cs\n+class C { }\n*** End Patch";

        var previews = await _tools.PreviewAsync(EditingAgentTools.ApplyPatchName, new Dictionary<string, object?> { ["input"] = input }, CancellationToken.None);
        var result = await Invoke(input);

        Assert.Equal([ProposedChangeKind.Edit, ProposedChangeKind.Create], previews.Select(preview => preview.Kind));
        Assert.Contains("Созданы файлы: src/C.cs", result, StringComparison.Ordinal);
        Assert.Equal("class C { }\n", _fixture.FileSystem.ReadAllText(EditorFixture.PathOf("src/C.cs")));
        Assert.Contains(_fixture.Editors.Tabs, tab => tab.Title == "C.cs");
    }

    private EditorTabViewModel Tab(string title) => _fixture.Editors.Tabs.OfType<EditorTabViewModel>().Single(tab => tab.Title == title);

    private async Task<string?> Invoke(string input) =>
        (await _patch.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["input"] = input }), TestContext.Current.CancellationToken))?.ToString();
}
