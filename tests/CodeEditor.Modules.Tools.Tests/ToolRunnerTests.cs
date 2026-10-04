using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Tools.Services;

namespace CodeEditor.Modules.Tools.Tests;

public sealed class ToolRunnerTests : IDisposable
{
    private const string Definition = "---\ndescription: d\nparameters:\n  path: p\n  dry-run: d\n---";

    private readonly ToolsFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void PowerShell_GetsDashParameters_AndVariables()
    {
        var folder = _fixture.AddTool("ps", Definition);

        var request = _fixture.Runner.Request(Tool("ps"), new Dictionary<string, string> { ["path"] = "src", ["dry-run"] = "1" }, forAgent: true);

        Assert.Equal(Path.Combine(ToolsFixture.Bin, "pwsh.exe"), request.FileName);
        Assert.Equal(["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(folder, "run.ps1"), "-path", "src", "-dry-run", "1"], request.Arguments);
        Assert.Equal(ToolsFixture.Root, request.WorkingDirectory);
        Assert.Equal("src", request.Environment["BREEZE_ARG_PATH"]);
        Assert.Equal("1", request.Environment["BREEZE_ARG_DRY_RUN"]);
        Assert.Equal(ToolsFixture.Root, request.Environment[ToolRunner.WorkspaceVariable]);
        Assert.True(request.HideSecretVariables);
        Assert.Equal("pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .breeze/tools/ps/run.ps1 -path src -dry-run 1", _fixture.Runner.Display(request));
    }

    [Fact]
    public void NodeAndCSharp_GetDoubleDashParameters_EmptyValuesAreSkipped()
    {
        _fixture.AddTool("js", Definition, "run.mjs");
        _fixture.AddTool("cs", Definition, "run.cs");
        var values = new Dictionary<string, string> { ["PATH"] = "my docs", ["dry-run"] = string.Empty };

        var node = _fixture.Runner.Request(Tool("js"), values, forAgent: false);
        var dotnet = _fixture.Runner.Request(Tool("cs"), values, forAgent: false);

        Assert.Equal(["--path", "my docs"], node.Arguments.Skip(1));
        Assert.Equal(["run", Tool("cs").Script, "--", "--path", "my docs"], dotnet.Arguments);
        Assert.False(node.Environment.ContainsKey("BREEZE_ARG_DRY_RUN"));
        Assert.EndsWith("--path \"my docs\"", _fixture.Runner.Display(node), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownParameter_IsExplained()
    {
        _fixture.AddTool("ps", Definition);

        var error = Assert.Throws<AgentToolException>(() => _fixture.Runner.Request(Tool("ps"), new Dictionary<string, string> { ["file"] = "x" }, forAgent: true));

        Assert.Contains("file", error.Message, StringComparison.Ordinal);
        Assert.Contains("path, dry-run", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingNode_IsExplained_WindowsPowerShellIsTheFallback()
    {
        _fixture.AddTool("js", Definition, "run.mjs");
        _fixture.AddTool("ps", Definition);
        _fixture.FileSystem.DeleteFile(Path.Combine(ToolsFixture.Bin, "node.exe"));
        _fixture.FileSystem.DeleteFile(Path.Combine(ToolsFixture.Bin, "pwsh.exe"));
        _fixture.FileSystem.AddFile(Path.Combine(ToolsFixture.Bin, "powershell.exe"));

        var error = Assert.Throws<AgentToolException>(() => _fixture.Runner.Request(Tool("js"), new Dictionary<string, string>(), forAgent: true));

        Assert.Contains("Node.js", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("powershell.exe", _fixture.Runner.Request(Tool("ps"), new Dictionary<string, string>(), forAgent: true).FileName, StringComparison.Ordinal);
    }

    private ToolDefinition Tool(string name) => _fixture.Shelf.Find(name)!;
}
