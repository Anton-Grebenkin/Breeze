using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Services.Memory;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Memory;

/// <summary>
/// Folder memory and snapshot (ADR 0012): typed, dated notes, the MEMORY.md index, the memory tool in any mode, and the
/// &lt;workspace&gt; and &lt;memory&gt; blocks only in the chat's first message and in the compaction summary.
/// </summary>
public sealed class ProjectMemoryTests : IDisposable
{
    private static readonly string MemoryFolder = Path.Combine(AgentFixture.Root, ".breeze", "agent", "memory");

    private readonly AgentFixture _fixture = new();

    public ProjectMemoryTests()
    {
        _fixture.FileSystem
            .AddFile(Path.Combine(AgentFixture.Root, "Shop.slnx"), "<Solution />")
            .AddFile(Path.Combine(AgentFixture.Root, "src", "Shop", "Shop.csproj"), "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")
            .AddFile(Path.Combine(AgentFixture.Root, "src", "Shop", "Order.cs"), "class Order { }")
            .AddFile(Path.Combine(AgentFixture.Root, "tests", "Shop.Tests", "Shop.Tests.csproj"), "<Project><ItemGroup><PackageReference Include=\"xunit.v3\" /></ItemGroup><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>")
            .AddFile(Path.Combine(AgentFixture.Root, "README.md"), "# Shop")
            .AddFile(Path.Combine(AgentFixture.Root, "docs", "design.md"), "# Design")
            .AddFile(Path.Combine(AgentFixture.Root, ".git", "HEAD"), "ref: refs/heads/feature/promo\n");
        _fixture.Workspace.Open(AgentFixture.Root);
        _fixture.Conversation.Tools.Add(new MemoryAgentTools(_fixture.Memory).CreateTools().Single());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Save_WritesNoteWithFrontMatter_AndIndex()
    {
        Assert.True(_fixture.Memory.Save("build-commands", "project", "How to build\nand test", "dotnet test --solution Shop.slnx"));
        Assert.False(_fixture.Memory.Save("build-commands", "project", "How to build and test", "dotnet test --solution Shop.slnx --no-build"));

        var note = _fixture.FileSystem.ReadAllText(Path.Combine(MemoryFolder, "build-commands.md"));
        var index = _fixture.FileSystem.ReadAllText(Path.Combine(MemoryFolder, ProjectMemory.IndexFileName));
        Assert.StartsWith("---\nname: build-commands\ntype: project\ndescription: How to build and test\nupdated: ", note, StringComparison.Ordinal);
        Assert.EndsWith("dotnet test --solution Shop.slnx --no-build\n", note, StringComparison.Ordinal);
        Assert.StartsWith("# Память агента", index, StringComparison.Ordinal);
        Assert.Contains("- [build-commands](build-commands.md) — How to build and test (project,", index, StringComparison.Ordinal);
        Assert.True(_fixture.FileSystem.FileExists(Path.Combine(AgentFixture.Root, ".breeze", "agent", ".gitignore")));
    }

    [Theory]
    [InlineData("Build Commands", "project", "имя")]
    [InlineData("build", "note", "тип")]
    public void Save_RejectsBadNameOrType(string name, string type, string what)
    {
        var error = Assert.Throws<AgentToolException>(() => _fixture.Memory.Save(name, type, "d", "c"));

        Assert.Contains(what == "имя" ? "Имя заметки" : "Тип заметки", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_MarksOldNotes_AndDeleteRemovesThem()
    {
        _fixture.FileSystem.AddFile(Path.Combine(MemoryFolder, "old-rule.md"), "---\nname: old-rule\ntype: feedback\ndescription: Old rule\nupdated: 2020-01-01\n---\n\nRule.");
        _fixture.Memory.Save("user-role", "user", "Senior .NET developer", "Prefers short answers.");

        var index = _fixture.Memory.IndexForModel()!;

        Assert.Equal(["- user-role (user", "- old-rule (feedback"], index.Split('\n').Select(line => line[..line.IndexOf(',')]));
        Assert.Contains("old-rule (feedback, updated 2020-01-01, may be outdated): Old rule", index, StringComparison.Ordinal);
        Assert.True(_fixture.Memory.Delete("old-rule"));
        Assert.False(_fixture.Memory.Delete("old-rule"));
        Assert.DoesNotContain("old-rule", _fixture.Memory.IndexForModel(), StringComparison.Ordinal);
    }

    // A read by the agent counts as use. A helper-model note unread for a month is recycled; an agent note only gets
    // a "may be outdated" mark (ADR 0029).
    [Fact]
    public void UnusedAutomaticNotes_AreForgotten_ReadAndExplicitOnesStay()
    {
        _fixture.FileSystem
            .AddFile(Path.Combine(MemoryFolder, "old-auto.md"), "---\nname: old-auto\ntype: project\ndescription: d\nupdated: 2020-01-01\nsource: auto\nused: 2020-01-01\nuses: 0\n---\n\nText.")
            .AddFile(Path.Combine(MemoryFolder, "old-manual.md"), "---\nname: old-manual\ntype: feedback\ndescription: d\nupdated: 2020-01-01\n---\n\nRule.")
            .AddFile(Path.Combine(MemoryFolder, "used-auto.md"), "---\nname: used-auto\ntype: project\ndescription: d\nupdated: 2020-01-01\nsource: auto\nused: 2020-01-01\nuses: 2\n---\n\nText.");

        var read = _fixture.Memory.Use("used-auto")!;
        var forgotten = _fixture.Memory.ForgetUnused();

        Assert.Equal((3, true), (read.Uses, read.IsAutomatic));
        Assert.Equal(["old-auto"], forgotten);
        Assert.Equal([Path.Combine(MemoryFolder, "old-auto.md")], _fixture.FileSystem.RecycledPaths);
        Assert.Equal(["old-manual", "used-auto"], _fixture.Memory.Notes().Select(note => note.Name));
        Assert.DoesNotContain("old-auto", _fixture.FileSystem.ReadAllText(Path.Combine(MemoryFolder, ProjectMemory.IndexFileName)), StringComparison.Ordinal);
    }

    // A save by the agent is explicit: the helper model may update such a note later but can never forget it.
    [Fact]
    public void SaveByTheAgent_MakesTheNoteExplicit_ForGood()
    {
        _fixture.Memory.Save("order-flow", "project", "d", "auto text", automatic: true);
        Assert.True(_fixture.Memory.Read("order-flow")!.IsAutomatic);

        _fixture.Memory.Save("order-flow", "project", "d", "agent text");
        _fixture.Memory.Save("order-flow", "project", "d", "auto again", automatic: true);

        Assert.False(_fixture.Memory.Read("order-flow")!.IsAutomatic);
    }

    // The snapshot and memory go only in the chat's first message, which is cached.
    [Fact]
    public async Task FirstMessage_CarriesWorkspaceAndMemory_OnlyOnce()
    {
        _fixture.Memory.Save("build-commands", "project", "How to build and test", "dotnet test");
        _fixture.Client.Reply("Первый.").Reply("Второй.");
        // The snapshot waits for the index only briefly; under full-suite load the scan can take longer.
        await _fixture.Index.WhenReady.WaitAsync(TestContext.Current.CancellationToken);

        await _fixture.SendAsync("что это за проект?");
        await _fixture.SendAsync("а тесты?");

        var first = Blocks(_fixture.Client.Requests[0][^1]);
        var workspace = first.Single(block => block.StartsWith("<workspace>", StringComparison.Ordinal));
        Assert.Contains("Solutions (1): Shop.slnx", workspace, StringComparison.Ordinal);
        Assert.Contains("Projects (2): src/Shop (net10.0), tests/Shop.Tests (net10.0, tests)", workspace, StringComparison.Ordinal);
        Assert.Contains("Docs (2): README.md, docs/design.md", workspace, StringComparison.Ordinal);
        Assert.Contains("Git branch: feature/promo", workspace, StringComparison.Ordinal);
        Assert.Contains("- build-commands (project, updated", first.Single(block => block.StartsWith("<memory>", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain(Blocks(_fixture.Client.Requests[1][^1]), block => block.StartsWith("<workspace>", StringComparison.Ordinal) || block.StartsWith("<memory>", StringComparison.Ordinal));
    }

    // "Remember" works in Ask mode too: memory isn't code.
    [Fact]
    public async Task MemoryTool_WorksInAskMode_AndShowsInFeed()
    {
        _fixture.Options.Set(new AgentOptions { Model = AgentFixture.TestModel, Mode = AgentMode.Ask });
        _fixture.Client
            .CallTool("m1", MemoryAgentTools.MemoryName, new Dictionary<string, object?> { ["action"] = "save", ["name"] = "no-comments", ["type"] = "feedback", ["description"] = "Code without extra comments", ["content"] = "Don't add comments.\nWhy: the user asked.\nHow to apply: only non-obvious places." })
            .Reply("Запомнил.");

        await _fixture.SendAsync("запомни: без лишних комментариев");

        Assert.Equal("Don't add comments.\nWhy: the user asked.\nHow to apply: only non-obvious places.", _fixture.Memory.Read("no-comments")!.Content);
        var row = new WorkflowToolPresenter().Present(new AgentToolCall(MemoryAgentTools.MemoryName, new Dictionary<string, object?> { ["action"] = "save", ["name"] = "no-comments" }, "ok"))!;
        Assert.Equal((AgentToolIcon.Memory, "Запомнено: no-comments"), (row.Icon, row.Title));
    }

    [Fact]
    public void SummaryState_CarriesMemoryAndWorkspace()
    {
        _fixture.Memory.Save("build-commands", "project", "How to build and test", "dotnet test");

        Assert.Contains("<memory>\n- build-commands (project", _fixture.Compaction.HarnessState(), StringComparison.Ordinal);
    }

    private static List<string> Blocks(ChatMessage message) => [.. message.Contents.OfType<TextContent>().Select(content => content.Text)];
}
