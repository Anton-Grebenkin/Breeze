using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Tools.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Tools.Tests;

/// <summary>run_tool: trust by code hash, the card with the code, the output to the model.</summary>
public sealed class ToolAgentToolsTests : IDisposable
{
    private const string Name = ToolAgentTools.RunToolName;

    private readonly ToolsFixture _fixture = new();
    private readonly string _folder;

    public ToolAgentToolsTests() =>
        _folder = _fixture.AddTool("count", "---\ndescription: Counts lines\nparameters:\n  path: Folder\n---", code: "param($path)\nWrite-Output 42");

    private ToolAgentTools Tools => _fixture.AgentTools;

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task NewTool_ShowsCodeOnCard_AndAlwaysAsks_UntilTrusted()
    {
        var arguments = Arguments();
        Assert.False(Tools.IsPreapproved(Name, arguments));
        Assert.True(Tools.AlwaysAsks(Name, arguments));

        var preview = Assert.Single(await Tools.PreviewAsync(Name, arguments, TestContext.Current.CancellationToken));

        Assert.Equal(ProposedChangeKind.Command, preview.Kind);
        Assert.StartsWith("pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .breeze/tools/count/run.ps1 -path src\n\nparam($path)\nWrite-Output 42", preview.NewText, StringComparison.Ordinal);
        Assert.Contains("count", preview.Title, StringComparison.Ordinal);
        Assert.Equal("count", Tools.SuggestRule(Name, arguments));

        Tools.AllowAlways(Name, "count");

        Assert.True(Tools.IsPreapproved(Name, arguments));
        Assert.False(Tools.AlwaysAsks(Name, arguments));
        Assert.Null(Tools.SuggestRule(Name, arguments));
    }

    [Fact]
    public async Task ChangedCode_AsksAgain_TrustSurvivesRestart()
    {
        await Tools.PreviewAsync(Name, Arguments(), TestContext.Current.CancellationToken);
        Tools.AllowAlways(Name, "count");
        var trust = new ToolTrust(_fixture.FileSystem, new UserDataPaths(ToolsFixture.UserData), NullLogger<ToolTrust>.Instance);
        Assert.True(trust.IsTrusted(_fixture.Shelf.Find("count")!));

        _fixture.FileSystem.AddFile(Path.Combine(_folder, "helper.ps1"), "Remove-Item -Recurse /");

        Assert.False(Tools.IsPreapproved(Name, Arguments()));
        Assert.True(Tools.AlwaysAsks(Name, Arguments()));
    }

    [Fact]
    public void PersonalTool_IsTrusted()
    {
        _fixture.AddTool("mine", "---\ndescription: mine\n---", personal: true);

        Assert.True(Tools.IsPreapproved(Name, new Dictionary<string, object?> { ["name"] = "mine" }));
    }

    [Fact]
    public async Task Run_ReturnsOutput_AndWritesOutputChannel()
    {
        _fixture.Processes.Returns(0, "42\n");

        var result = await InvokeAsync(new() { ["name"] = "count", ["parameters"] = new Dictionary<string, string> { ["path"] = "src" } });

        Assert.Equal("42", result);
        var request = Assert.Single(_fixture.Processes.Requests);
        Assert.Equal(["-path", "src"], request.Arguments.TakeLast(2));
        Assert.Contains("> pwsh", _fixture.Output.Text("Инструменты"), StringComparison.Ordinal);
        Assert.Equal("Инструмент count завершён — вывод в панели «Вывод».", _fixture.StatusBar.Message);
    }

    [Fact]
    public async Task FailedRun_And_UnknownTool_AreErrorsForTheModel()
    {
        _fixture.Processes.Returns(2, "boom");

        var failed = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["name"] = "count" }));
        var missing = await Assert.ThrowsAsync<AgentToolException>(() => InvokeAsync(new() { ["name"] = "deploy" }));

        Assert.Contains("2", failed.Message, StringComparison.Ordinal);
        Assert.Contains("boom", failed.Message, StringComparison.Ordinal);
        Assert.Contains("count", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Context_ListsShelfWithParameters()
    {
        var lines = await new ToolAgentContext(_fixture.Shelf).GetContextAsync(new AgentContextRequest(false), TestContext.Current.CancellationToken);

        Assert.Equal("Tool shelf (call with run_tool):\n- count: Counts lines. Parameters: path — Folder.", Assert.Single(lines));
    }

    private static Dictionary<string, object?> Arguments() =>
        new() { ["name"] = "count", ["parameters"] = new Dictionary<string, string> { ["path"] = "src" } };

    private async Task<string> InvokeAsync(Dictionary<string, object?> arguments)
    {
        var tool = Tools.CreateTools().OfType<AIFunction>().Single();
        return (await tool.InvokeAsync(new AIFunctionArguments(arguments), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
    }
}
