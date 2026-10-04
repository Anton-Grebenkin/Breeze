using CodeEditor.Core.Context;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Rules;

/// <summary>agent.md before a tool call: protected paths, ask_before actions, rules for some files.</summary>
public sealed class RuleGuardTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\project");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Path.Combine(Root, ".breeze", "rules"));
    private readonly Core.Files.Workspace _workspace;
    private readonly ProjectRuleGuard _guard;

    public RuleGuardTests()
    {
        _workspace = new Core.Files.Workspace(_fileSystem, new ContextKeyService(), NullLogger<Core.Files.Workspace>.Instance);
        _guard = new ProjectRuleGuard(new ProjectInstructions(_workspace, _fileSystem, new UserDataPaths(Path.GetFullPath(@"C:\userdata"))), _workspace);
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "agent.md"),
            "---\nprotected: [\"src/Platform/**\"]\nask_before: [new-package, public-api, delete-tests, migration]\n---\nrules");
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "rules", "xaml.md"), "---\napplies: \"*.xaml\"\n---\nOnly DynamicResource.");
        _workspace.Open(Root);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void ProtectedPath_NeedsDecision()
    {
        Assert.Contains("src/Platform/Core/A.cs", Ask(Edit("src/Platform/Core/A.cs", "a", "b")), StringComparison.Ordinal);
        Assert.Null(Ask(Edit("src/Modules/A.cs", "a", "b")));
    }

    [Fact]
    public void MovingIntoProtectedFolder_NeedsDecision() =>
        Assert.NotNull(Ask(new FileChangePreview(ProposedChangeKind.Move, "A.cs", string.Empty, string.Empty) { NewRelativePath = "src/Platform/A.cs" }));

    [Theory]
    [InlineData("<Project>\n</Project>", "<Project>\n  <PackageReference Include=\"Polly\" />\n</Project>", true)]
    [InlineData("<Project>\n  <PackageReference Include=\"Polly\" />\n</Project>", "<Project>\n  <PackageReference Include=\"Polly\" />\n  <Nullable>enable</Nullable>\n</Project>", false)]
    public void PackageReference_InProjectFile(string before, string after, bool asks) =>
        Assert.Equal(asks, Ask(Edit("src/App/App.csproj", before, after)) is not null);

    [Theory]
    [InlineData("{\n  \"version\": \"1.0.0\"\n}", "{\n  \"version\": \"1.0.1\"\n}", false)]
    [InlineData("{\n  \"dependencies\": {\n  }\n}", "{\n  \"dependencies\": {\n    \"left-pad\": \"^1.3.0\"\n  }\n}", true)]
    public void Dependency_InPackageJson(string before, string after, bool asks) =>
        Assert.Equal(asks, Ask(Edit("web/package.json", before, after)) is not null);

    [Theory]
    [InlineData("dotnet add src/App package Polly", true)]
    [InlineData("npm install left-pad", true)]
    [InlineData("npm install", false)]
    [InlineData("dotnet ef migrations add Init", true)]
    [InlineData("dotnet build", false)]
    public void Commands_AreRecognized(string command, bool asks)
    {
        var preview = new FileChangePreview(ProposedChangeKind.Command, ".", string.Empty, command);

        Assert.Equal(asks, Ask(preview) is not null);
        Assert.Equal(asks, _guard.AskReason(new Dictionary<string, object?> { ["command"] = command }, []) is not null);
    }

    [Fact]
    public void PublicApi_DeletedTests_AndMigrations()
    {
        Assert.NotNull(Ask(Edit("src/Modules/X.Contracts/IFoo.cs", "a", "b")));
        Assert.NotNull(Ask(new FileChangePreview(ProposedChangeKind.Delete, "tests/X.Tests/FooTests.cs", "class FooTests {}", string.Empty)));
        Assert.NotNull(Ask(Edit("src/Foo.cs", "[Fact]\nvoid A() {}\n[Fact]\nvoid B() {}", "[Fact]\nvoid A() {}")));
        Assert.NotNull(Ask(new FileChangePreview(ProposedChangeKind.Create, "src/Data/Migrations/0001_Init.cs", string.Empty, "class Init {}")));
        Assert.Null(Ask(new FileChangePreview(ProposedChangeKind.Delete, "src/Foo.cs", "class Foo {}", string.Empty)));
    }

    [Fact]
    public void WithoutAskBefore_CategoryIsIgnored()
    {
        _fileSystem.AddFile(Path.Combine(Root, ".breeze", "agent.md"), "rules only");

        Assert.Null(Ask(Edit("src/Modules/X.Contracts/IFoo.cs", "a", "b")));
    }

    [Fact]
    public void FileRule_ComesWithFirstRead_Once()
    {
        var first = _guard.RulesForRead("src/Views/Main.xaml");

        Assert.Contains("<project_rule source=\".breeze/rules/xaml.md\" applies=\"*.xaml\">\nOnly DynamicResource.\n</project_rule>", first, StringComparison.Ordinal);
        Assert.Null(_guard.RulesForRead(Path.Combine(Root, "src", "Views", "Other.xaml")));
        Assert.Null(_guard.RulesBeforeEdit([Edit("src/Views/Main.xaml", "a", "b")]));
        Assert.Null(_guard.RulesForRead("src/A.cs"));
    }

    [Fact]
    public void EditWithoutRead_GetsRule_ThenPasses_NewChatStartsOver()
    {
        Assert.Contains("Only DynamicResource.", _guard.RulesBeforeEdit([Edit("Main.xaml", "a", "b")]), StringComparison.Ordinal);
        Assert.Null(_guard.RulesBeforeEdit([Edit("Main.xaml", "a", "b")]));

        _guard.Reset();

        Assert.NotNull(_guard.RulesForRead("./Main.xaml"));
    }

    private string? Ask(FileChangePreview change) => _guard.AskReason(new Dictionary<string, object?>(), [change]);

    private static FileChangePreview Edit(string path, string before, string after) => new(ProposedChangeKind.Edit, path, before, after);
}
