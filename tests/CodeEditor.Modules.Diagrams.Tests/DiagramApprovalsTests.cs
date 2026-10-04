using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Diagrams.Services.Agent;
using CodeEditor.Modules.Diagrams.Tests.Infrastructure;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>
/// <c>diagram</c> approvals (ADR 0035): check and view never ask; save asks only before overwriting a file the agent
/// didn't write; a read-only agent may only check.
/// </summary>
public sealed class DiagramApprovalsTests : IDisposable
{
    private readonly DiagramsFixture _fixture = new();
    private readonly DiagramRenderTargets _targets;
    private readonly DiagramApprovals _approvals;

    public DiagramApprovalsTests()
    {
        _fixture.AddFile("docs/arch.mmd", "graph TD").AddFile("docs/README.md", "```mermaid\npie\n```");
        _targets = new DiagramRenderTargets(_fixture.Workspace, _fixture.FileSystem, _fixture.Writes);
        _approvals = new DiagramApprovals(_targets);
    }

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData("check", true, true)]
    [InlineData("view", true, false)]
    [InlineData("render", true, false)]
    public void ReadingAndNewFiles_GoWithoutAsking(string action, bool preapproved, bool readOnly)
    {
        var call = Call(action, "docs/arch.mmd");

        Assert.Equal((preapproved, readOnly), (_approvals.IsPreapproved(DiagramAgentTools.ToolName, call), _approvals.IsReadOnly(DiagramAgentTools.ToolName, call)));
        Assert.Null(_approvals.SuggestRule(DiagramAgentTools.ToolName, call));
    }

    // A person may have drawn an image with the same name: overwrite only after a card listing the files.
    [Fact]
    public async Task ForeignFile_NeedsTheCard_WithItsName()
    {
        _fixture.AddFile("docs/arch.svg", "<svg>рисунок человека</svg>");
        var call = Call("render", "docs/arch.mmd");

        Assert.False(_approvals.IsPreapproved(DiagramAgentTools.ToolName, call));
        var preview = Assert.Single(await Tools().PreviewAsync(DiagramAgentTools.ToolName, call, TestContext.Current.CancellationToken));
        Assert.Equal((ProposedChangeKind.Command, "docs/arch.svg"), (preview.Kind, preview.NewText));
        Assert.Equal("Агент хочет сохранить схему поверх файлов", preview.Title);
    }

    // The agent updates its own earlier export without asking; otherwise every diagram edit would need a new card.
    [Fact]
    public void OwnExport_IsOverwrittenWithoutAsking()
    {
        _fixture.AddFile("docs/arch.png", "png");
        _fixture.Writes.Record(DiagramsFixture.PathOf("docs/arch.png"));

        Assert.True(_approvals.IsPreapproved(DiagramAgentTools.ToolName, Call("render", "docs/arch.mmd", format: "png")));
    }

    // Markdown without a block number: any foreign README-N.svg is a reason to ask, even if there are fewer blocks now.
    [Fact]
    public void MarkdownExports_AreCheckedByName()
    {
        _fixture.AddFile("docs/README-7.svg", "<svg/>").AddFile("docs/README-notes.svg", "<svg/>");

        Assert.False(_approvals.IsPreapproved(DiagramAgentTools.ToolName, Call("render", "docs/README.md")));
        Assert.True(_approvals.IsPreapproved(DiagramAgentTools.ToolName, Call("render", "docs/README.md", block: 1)));
        Assert.Equal([DiagramsFixture.PathOf("docs/README-7.svg")], _targets.Foreign(Call("render", "docs/README.md")));
    }

    [Fact]
    public void OtherTools_AreNotDecided() => Assert.False(_approvals.CanDecide("git_change"));

    private DiagramAgentTools Tools() => new(
        [_fixture.Renderer],
        new DiagramToolInputs(_fixture.Workspace, _fixture.Reader),
        _targets,
        _fixture.FileSystem,
        _fixture.Workspace,
        _fixture.Writes,
        new FakeAgentImages());

    private static Dictionary<string, object?> Call(string action, string path, string? format = null, int? block = null) =>
        new() { ["action"] = action, ["path"] = path, ["format"] = format, ["block"] = block };
}
