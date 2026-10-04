using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Services.Chat;
using CodeEditor.Modules.Agent.Tests.Infrastructure;

namespace CodeEditor.Modules.Agent.Tests.Chat;

/// <summary>
/// What the user did to the agent's files between messages goes into <c>&lt;context&gt;</c>: changed, deleted or
/// rejected edits. Each file is named once, and the agent must reread it before editing.
/// </summary>
public sealed class UserChangesContextTests : IDisposable
{
    private static readonly string A = Path.Combine(AgentFixture.Root, "src", "A.cs");
    private static readonly string B = Path.Combine(AgentFixture.Root, "src", "B.cs");
    private readonly AgentFixture _fixture = new();
    private readonly UserChangesAgentContext _context;

    public UserChangesContextTests()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.FileSystem.AddFile(A, "class A { }").AddFile(B, "class B { }");
        _context = new UserChangesAgentContext(_fixture.FileState, _fixture.Changes, _fixture.Workspace);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task FileChangedByUser_ReportedOnce_AndMustBeReadAgain()
    {
        _fixture.FileState.RecordRead(A, "class A { }", 1, 1);
        _fixture.FileState.RecordRead(B, "class B { }", 1, 1);
        _fixture.FileSystem.AddFile(A, "class A { int X; }");

        var line = Assert.Single(await ContextAsync());

        Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.UserChangedFiles, "src/A.cs"), line);
        Assert.NotNull(_fixture.FileState.CheckEditable(A, "src/A.cs", "class A { int X; }"));
        Assert.Null(_fixture.FileState.CheckEditable(B, "src/B.cs", "class B { }"));
        Assert.Empty(await ContextAsync());
    }

    [Fact]
    public async Task RejectedAndDeleted_AreMarked()
    {
        _fixture.FileState.RecordWrite(A, "class A { int X; }", "class A { }");
        _fixture.FileState.RecordRejected(A);
        _fixture.FileState.RecordRead(B, "class B { }", 1, 1);
        _fixture.FileSystem.DeleteFile(B);

        var line = Assert.Single(await ContextAsync());

        Assert.Contains($"A.cs ({Strings.UserRejectedChanges})", line, StringComparison.Ordinal);
        Assert.Contains($"B.cs ({Strings.UserDeletedFile})", line, StringComparison.Ordinal);
    }

    // The file holds the agent's own edit, a version it knows, so there's nothing to report.
    [Fact]
    public async Task AgentsOwnVersions_NothingToSay()
    {
        _fixture.FileState.RecordRead(A, "class A { }", 1, 1);
        _fixture.FileState.RecordWrite(B, "class B { int Y; }", "class B { }");
        _fixture.FileSystem.AddFile(B, "class B { int Y; }");

        Assert.Empty(await ContextAsync());
    }

    private async Task<IReadOnlyList<string>> ContextAsync() =>
        await _context.GetContextAsync(new AgentContextRequest(IncludeActiveEditor: false, "вопрос"), TestContext.Current.CancellationToken);
}
