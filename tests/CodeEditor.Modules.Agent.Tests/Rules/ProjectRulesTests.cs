using CodeEditor.Core.Context;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Rules;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Rules;

/// <summary>Reading agent.md, AGENTS.md, CLAUDE.md, rules for some files and personal rules.</summary>
public sealed class ProjectRulesTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(@"C:\project");
    private static readonly string UserData = Path.GetFullPath(@"C:\userdata");

    private readonly FakeFileSystem _fileSystem = new FakeFileSystem().AddDirectory(Root).AddDirectory(UserData).AddDirectory(Path.Combine(Root, ".breeze"));
    private readonly Core.Files.Workspace _workspace;
    private readonly ProjectInstructions _instructions;

    public ProjectRulesTests()
    {
        _workspace = new Core.Files.Workspace(_fileSystem, new ContextKeyService(), NullLogger<Core.Files.Workspace>.Instance);
        _instructions = new ProjectInstructions(_workspace, _fileSystem, new UserDataPaths(UserData));
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void AgentFile_HeaderBecomesSettings()
    {
        AddRules(".breeze/agent.md", "---\nprotected: [\"src/Platform/**\", \"**/*.csproj\"]\nask_before: [new-package, migration]\n---\n# Rules\n- Use Result.");

        var rules = Load();

        Assert.Equal(["src/Platform/**", "**/*.csproj"], rules.ProtectedPatterns);
        Assert.True(rules.Protected.Matches("src/Platform/CodeEditor.Core/A.cs"));
        Assert.True(rules.Protected.Matches("tests/X/X.csproj"));
        Assert.False(rules.Protected.Matches("src/Modules/A.cs"));
        Assert.Equal(["migration", "new-package"], rules.AskBefore.Order(StringComparer.Ordinal));
        Assert.Equal("# Rules\n- Use Result.", Assert.Single(rules.Sources).Text);
        Assert.Empty(rules.Problems);
    }

    [Fact]
    public void UnknownSettingsAndCategories_AreProblems()
    {
        AddRules(".breeze/agent.md", "---\nprocess: auto\nask_before: [new-package, everything]\n---\ntext");

        var rules = Load();

        Assert.Equal(2, rules.Problems.Count);
        Assert.Contains(rules.Problems, problem => problem.Contains("process", StringComparison.Ordinal));
        Assert.Contains(rules.Problems, problem => problem.Contains("everything", StringComparison.Ordinal));
        Assert.Equal(["new-package"], rules.AskBefore);
    }

    [Fact]
    public void SameTextInAgentsAndClaude_IsSentOnce()
    {
        AddRules("AGENTS.md", "общие правила");
        AddRules("CLAUDE.md", "общие правила");

        Assert.Equal("AGENTS.md", Assert.Single(Load().Sources).Source);
    }

    [Fact]
    public void PersonalRules_GoAfterFolderRules()
    {
        _fileSystem.AddFile(Path.Combine(UserData, "agent.md"), "---\nask_before: [delete-tests]\n---\nличные правила");
        AddRules("CLAUDE.md", "правила папки");

        var rules = Load();

        Assert.Equal([("CLAUDE.md", false), ("agent.md", true)], rules.Sources.Select(source => (source.Source, source.IsPersonal)));
        Assert.Contains("delete-tests", rules.AskBefore.AsEnumerable());
        Assert.Contains("<personal_rules source=\"agent.md\">\nличные правила\n</personal_rules>", RulesPrompt.Build(rules), StringComparison.Ordinal);
    }

    [Fact]
    public void PersonalRules_WorkWithoutFolder()
    {
        _fileSystem.AddFile(Path.Combine(UserData, "agent.md"), "личные правила");

        Assert.Equal("личные правила", Assert.Single(_instructions.Load().Sources).Text);
    }

    [Fact]
    public void RulesForSomeFiles_AreIndexed_GeneralOnesGoToPrompt()
    {
        _fileSystem.AddDirectory(Path.Combine(Root, ".breeze", "rules"));
        AddRules(".breeze/rules/xaml.md", "---\napplies: \"src/**/*.xaml\"\ndescription: XAML styles\n---\nOnly DynamicResource.");
        AddRules(".breeze/rules/general.md", "Commit messages in Russian.");

        var rules = Load();
        var prompt = RulesPrompt.Build(rules);

        var scoped = Assert.Single(rules.Scoped);
        Assert.Equal((".breeze/rules/xaml.md", "XAML styles", "Only DynamicResource."), (scoped.Source, scoped.Description, scoped.Text));
        Assert.True(scoped.Filter.Matches("src/App/Views/Main.xaml"));
        Assert.Contains("- .breeze/rules/xaml.md (src/**/*.xaml): XAML styles", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Only DynamicResource", prompt, StringComparison.Ordinal);
        Assert.Contains("Commit messages in Russian.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void LimitCountsAllFiles_LaterFilesAreCutOrSkipped()
    {
        AddRules("AGENTS.md", string.Join('\n', Enumerable.Range(1, 150).Select(line => $"agents {line}")));
        AddRules("CLAUDE.md", string.Join('\n', Enumerable.Range(1, 100).Select(line => $"claude {line}")));
        _fileSystem.AddFile(Path.Combine(UserData, "agent.md"), "личные");

        var rules = Load();

        Assert.Equal(2, rules.Sources.Count);
        Assert.Contains("claude 50", rules.Sources[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("claude 51", rules.Sources[1].Text, StringComparison.Ordinal);
        Assert.Contains(rules.Problems, problem => problem.StartsWith("agent.md", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentFile_IsCreatedFromTemplate_AndParsesWithoutProblems()
    {
        _workspace.Open(Root);

        var path = _instructions.EnsureAgentFile();

        Assert.Equal(Path.Combine(Root, ".breeze", "agent.md"), path);
        Assert.Contains("protected", _fileSystem.ReadAllText(path!), StringComparison.Ordinal);
        Assert.Empty(_instructions.Load().Problems);
        Assert.Empty(_instructions.Load().ProtectedPatterns);
    }

    [Fact]
    public void UnchangedFiles_AreNotReadAgain_ChangedOnesAre()
    {
        AddRules("AGENTS.md", "first");

        var first = Load();

        Assert.Same(first, _instructions.Load());
        AddRules("AGENTS.md", "second version");
        Assert.Equal("second version", Assert.Single(_instructions.Load().Sources).Text);
        AddRules("CLAUDE.md", "added");
        Assert.Equal(2, _instructions.Load().Sources.Count);
    }

    [Theory]
    [InlineData(".breeze/agent.md", true)]
    [InlineData(".breeze/rules/xaml.md", true)]
    [InlineData("AGENTS.md", true)]
    [InlineData(".breeze/settings.json", false)]
    [InlineData("docs/AGENTS.md", false)]
    public void RulesFiles_AreRecognized(string path, bool expected) => Assert.Equal(expected, ProjectInstructions.IsRulesFile(path));

    private void AddRules(string relativePath, string text) => _fileSystem.AddFile(Path.Combine(Root, relativePath), text);

    private ProjectRuleSet Load()
    {
        _workspace.Open(Root);
        return _instructions.Load();
    }
}
