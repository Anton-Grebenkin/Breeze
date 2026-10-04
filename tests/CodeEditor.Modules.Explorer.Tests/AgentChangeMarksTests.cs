using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Modules.Explorer.ViewModels;
using CodeEditor.Testing;

namespace CodeEditor.Modules.Explorer.Tests;

/// <summary>Files with agent edits awaiting review and their folders are highlighted in the tree (ADR 0040).</summary>
public sealed class AgentChangeMarksTests : IDisposable
{
    private readonly ExplorerFixture _fixture = new();
    private readonly AgentFileState _fileState = new();
    private readonly AgentChangeMarks _marks;
    private readonly ExplorerViewModel _explorer;

    public AgentChangeMarksTests()
    {
        _marks = new AgentChangeMarks(_fileState);
        _explorer = new ExplorerViewModel(_fixture.Workspace, _fixture.FileSystem, _fixture.CommandService, _fixture.Context, new InlineUiDispatcher(), _marks);
    }

    public void Dispose()
    {
        _explorer.Dispose();
        _marks.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task ChangedFile_AndItsFolders_AreMarked_UntilAccepted()
    {
        var program = Path.Combine(ExplorerFixture.Root, "src", "Program.cs");
        await OpenAsync();
        var src = Node("src");

        _fileState.RecordWrite(program, "new", "old");
        await _explorer.Tree!.LoadChildrenAsync(src);

        Assert.True(src.ContainsAgentChanges);
        Assert.True(_explorer.Tree.Root.ContainsAgentChanges);
        Assert.True(Child(src, "Program.cs").IsAgentChanged);
        Assert.False(Child(src, "file2.cs").IsAgentChanged);
        Assert.False(Node("docs").ContainsAgentChanges);

        _fileState.AcceptFile(program);

        Assert.False(src.ContainsAgentChanges);
        Assert.False(Child(src, "Program.cs").IsAgentChanged);
    }

    [Fact]
    public async Task NodesLoadedLater_TakeTheMarks()
    {
        _fileState.RecordWrite(Path.Combine(ExplorerFixture.Root, "docs", "guide.md"), "new", "old");
        await OpenAsync();
        var docs = Node("docs");

        await _explorer.Tree!.LoadChildrenAsync(docs);

        Assert.True(docs.ContainsAgentChanges);
        Assert.True(Child(docs, "guide.md").IsAgentChanged);
    }

    private async Task OpenAsync()
    {
        _fixture.Workspace.Open(ExplorerFixture.Root);
        await _explorer.RootLoaded;
    }

    private FileNodeViewModel Node(string name) => _explorer.Tree!.Root.Children.Single(node => node.Name == name);

    private static FileNodeViewModel Child(FileNodeViewModel folder, string name) => folder.Children.Single(node => node.Name == name);
}
