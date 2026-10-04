using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Explorer.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Explorer.Tests;

public sealed class ExplorerEditingToolsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\repo");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem()
        .AddDirectory(Root)
        .AddFile(Path.Combine(Root, "src", "Old.cs"), "class Old { }")
        .AddFile(Path.Combine(Root, "README.md"), "# readme");

    private readonly AgentFileState _fileState = new();
    private readonly Workspace _workspace;
    private readonly ExplorerEditingTools _tools;
    private readonly Dictionary<string, AIFunction> _functions;

    public ExplorerEditingToolsTests()
    {
        _workspace = new Workspace(_fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _workspace.Open(Root);
        _tools = new ExplorerEditingTools(_workspace, _fileSystem, _fileState);
        _functions = _tools.CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Tools_RequireApproval() => Assert.All(_functions.Values, function => Assert.IsType<ApprovalRequiredAIFunction>(function));

    [Fact]
    public async Task Delete_GoesToRecycleBin_WithPreviewOfContent()
    {
        var preview = Assert.Single(await _tools.PreviewAsync(ExplorerEditingTools.DeleteFileName, new Dictionary<string, object?> { ["path"] = "README.md" }, TestContext.Current.CancellationToken));
        Assert.Equal((ProposedChangeKind.Delete, "README.md", "# readme"), (preview.Kind, preview.RelativePath, preview.OldText));

        await Invoke(ExplorerEditingTools.DeleteFileName, new() { ["path"] = "README.md" });

        Assert.Contains(Path.Combine(Root, "README.md"), _fileSystem.RecycledPaths);
    }

    [Fact]
    public async Task Move_CreatesFolder_RecordsForRevert_AndRefusesToOverwrite()
    {
        var preview = Assert.Single(await _tools.PreviewAsync(ExplorerEditingTools.MoveFileName, new Dictionary<string, object?> { ["from"] = "src/Old.cs", ["to"] = "lib/New.cs" }, TestContext.Current.CancellationToken));
        Assert.Equal(("src/Old.cs", "lib/New.cs"), (preview.RelativePath, preview.NewRelativePath));

        await Invoke(ExplorerEditingTools.MoveFileName, new() { ["from"] = "src/Old.cs", ["to"] = "lib/New.cs" });

        Assert.True(_fileSystem.FileExists(Path.Combine(Root, "lib", "New.cs")));
        Assert.False(_fileSystem.FileExists(Path.Combine(Root, "src", "Old.cs")));
        Assert.Equal(new Dictionary<string, string?> { [Path.Combine(Root, "src", "Old.cs")] = "class Old { }", [Path.Combine(Root, "lib", "New.cs")] = null }, _fileState.Changes);
        Assert.Contains("уже существует", (await Assert.ThrowsAsync<AgentToolException>(() =>
            Invoke(ExplorerEditingTools.MoveFileName, new() { ["from"] = "lib/New.cs", ["to"] = "README.md" }))).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("../outside.txt")]
    [InlineData("missing.cs")]
    public async Task Delete_RefusesRootOutsideAndMissing(string path) =>
        await Assert.ThrowsAsync<AgentToolException>(() => Invoke(ExplorerEditingTools.DeleteFileName, new() { ["path"] = path }));

    private async Task<string?> Invoke(string tool, Dictionary<string, object?> arguments) =>
        (await _functions[tool].InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString();
}
