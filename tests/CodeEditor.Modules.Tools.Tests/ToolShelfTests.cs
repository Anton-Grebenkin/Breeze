using CodeEditor.Core.Files;
using CodeEditor.Modules.Tools.Services;

namespace CodeEditor.Modules.Tools.Tests;

public sealed class ToolShelfTests : IDisposable
{
    private readonly ToolsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void ReadsDescriptionParametersScriptAndTimeout()
    {
        _fixture.AddTool("count-lines", "---\ndescription: Counts lines\nparameters:\n  path: Folder\n  pattern: Mask\ntimeout: 30\n---\nNotes", "run.mjs");

        var tool = Assert.Single(_fixture.Shelf.Tools);

        Assert.Equal(("count-lines", "Counts lines", ToolScriptKind.Node, false), (tool.Name, tool.Description, tool.Kind, tool.IsPersonal));
        Assert.Equal([new ToolParameter("path", "Folder"), new ToolParameter("pattern", "Mask")], tool.Parameters);
        Assert.Equal(TimeSpan.FromSeconds(30), tool.Timeout);
        Assert.EndsWith("run.mjs", tool.Script, StringComparison.Ordinal);
        Assert.Empty(_fixture.Shelf.Problems);
    }

    [Fact]
    public void DescriptionFallsBackToFirstLine_ParametersMayBeAList()
    {
        _fixture.AddTool("lint", "---\nparameters: [path, fix]\n---\n# Runs the linter\nmore", "run.cs");

        var tool = Assert.Single(_fixture.Shelf.Tools);

        Assert.Equal(("Runs the linter", ToolScriptKind.CSharp, "path, fix"), (tool.Description, tool.Kind, tool.Signature));
    }

    [Fact]
    public void FolderTool_WinsOverPersonalOne_PersonalAreListed()
    {
        _fixture.AddTool("deploy", "---\ndescription: personal\n---", personal: true);
        _fixture.AddTool("notes", "---\ndescription: personal notes\n---", personal: true);
        _fixture.AddTool("deploy", "---\ndescription: folder\n---");

        Assert.Equal([("deploy", "folder", false), ("notes", "personal notes", true)],
            _fixture.Shelf.Tools.Select(tool => (tool.Name, tool.Description, tool.IsPersonal)));
    }

    [Fact]
    public void BrokenTools_AreProblems()
    {
        _fixture.AddTool("no-script", "---\ndescription: x\n---", script: string.Empty);
        _fixture.AddTool("bad-run", "---\nrun: ../evil.ps1\n---");
        _fixture.AddTool("odd", "---\ndescription: x\nparameters:\n  1bad: y\ncolor: red\ntimeout: 9999\n---");

        Assert.Equal(["odd"], _fixture.Shelf.Tools.Select(tool => tool.Name));
        Assert.Equal(5, _fixture.Shelf.Problems.Length);
        Assert.Equal(ToolDefinition.DefaultTimeout, _fixture.Shelf.Tools[0].Timeout);
    }

    [Fact]
    public void ChangedToolFile_RaisesChanged_OtherFilesDoNot()
    {
        var folder = _fixture.AddTool("count", "---\ndescription: one\n---");
        var changes = 0;
        _fixture.Shelf.Changed += (_, _) => changes++;
        var watcher = _fixture.FileSystem.Watchers.Single();

        _fixture.FileSystem.AddFile(Path.Combine(folder, "tool.md"), "---\ndescription: two\n---");
        watcher.Raise(new FileChange(Path.Combine(folder, "tool.md"), FileChangeKind.Changed));
        watcher.Raise(new FileChange(Path.Combine(ToolsFixture.Root, "src", "a.cs"), FileChangeKind.Changed));

        Assert.Equal(1, changes);
        Assert.Equal("two", _fixture.Shelf.Find("COUNT")?.Description);
    }

    [Theory]
    [InlineData("count-lines", true)]
    [InlineData("a.b_c", true)]
    [InlineData("-x", false)]
    [InlineData("two words", false)]
    [InlineData("..", false)]
    public void ToolNames(string name, bool valid) => Assert.Equal(valid, ToolDefinitionReader.IsValidName(name));
}
