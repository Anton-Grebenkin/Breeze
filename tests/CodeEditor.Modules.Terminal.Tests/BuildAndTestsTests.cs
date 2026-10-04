using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Terminal.Services.Build;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Tests;

public sealed class BuildAndTestsTests : IDisposable
{
    private readonly TerminalFixture _fixture = new();
    private readonly RecordingVerificationLog _verification = new();
    private readonly Dictionary<string, AIFunction> _tools;

    public BuildAndTestsTests() =>
        _tools = new BuildAgentTools(_fixture.Build, _fixture.Tests, _fixture.Workspace, _verification).CreateTools().OfType<AIFunction>().ToDictionary(tool => tool.Name);

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Target_PrefersSolution_ThenSingleProject()
    {
        Assert.Equal(TerminalFixture.PathOf("App.slnx"), _fixture.Target.Resolve(null));
        Assert.Equal(TerminalFixture.PathOf("src/App/App.csproj"), _fixture.Target.Resolve("src/App/App.csproj"));
        Assert.Contains("не решение и не проект", Assert.Throws<AgentToolException>(() => _fixture.Target.Resolve("src/App/A.cs")).Message, StringComparison.Ordinal);
        // A folder resolves to its only project, as with dotnet test; a folder without one is the same error.
        Assert.Equal(TerminalFixture.PathOf("tests/App.Tests/App.Tests.csproj"), _fixture.Target.Resolve("tests/App.Tests"));
        Assert.Contains("не решение и не проект", Assert.Throws<AgentToolException>(() => _fixture.Target.Resolve("tests")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_RunsDotnetInEnglish_ParsesAndRemembers()
    {
        _fixture.Runner.Returns(1, @"C:\repo\src\App\A.cs(12,5): error CS1002: ; expected [C:\repo\src\App\App.csproj]");

        var result = await Invoke("build");

        var request = Assert.Single(_fixture.Runner.Requests);
        Assert.Equal("dotnet", request.FileName);
        Assert.Equal(["build", TerminalFixture.PathOf("App.slnx"), "-nologo", "-v:q", "-clp:NoSummary", "-p:GenerateFullPaths=true"], request.Arguments);
        Assert.Equal("en", request.Environment["DOTNET_CLI_UI_LANGUAGE"]);
        Assert.Equal(TerminalFixture.Root, request.WorkingDirectory);
        Assert.Contains("src/App/A.cs(12,5): error CS1002: ; expected", result, StringComparison.Ordinal);
        Assert.Contains("> dotnet build App.slnx", _fixture.Output.GetOrCreate(DotNetBuild.ChannelName).Snapshot(), StringComparison.Ordinal);
        Assert.Equal(1, _fixture.Build.Last!.Errors);
    }

    [Fact]
    public async Task Build_SavesEditedTabsFirst_SoItChecksCurrentCode()
    {
        var path = TerminalFixture.PathOf("src/App/A.cs");
        var document = await _fixture.Documents.OpenAsync(path, TestContext.Current.CancellationToken);
        document.Buffer.Replace(0, 0, "// правка агента\n");
        _fixture.Runner.Returns(0, string.Empty);

        await Invoke("build");

        Assert.Equal("// правка агента\nclass A { }", _fixture.FileSystem.ReadAllText(path));
        Assert.False(document.IsDirty);
    }

    [Fact]
    public async Task BuildAndTests_ReportToVerificationGate_AndAppendItsHint()
    {
        _fixture.Runner.Returns(1, @"C:\repo\src\App\A.cs(1,1): error CS1: a");
        _verification.Hint = "\n\nподсказка";

        var build = await Invoke("build");
        await _fixture.Index.WhenReady;
        _fixture.Runner.Returns(0, "Test run summary: Passed!\n  total: 1\n  failed: 0\n  succeeded: 1\n  skipped: 0");
        await Invoke("run_tests");

        Assert.EndsWith("\n\nподсказка", build, StringComparison.Ordinal);
        Assert.Equal([(VerificationKind.Build, false), (VerificationKind.Tests, true)], _verification.Records);
    }

    [Fact]
    public async Task Verifier_SeesSolutionAndUnitTests()
    {
        await _fixture.Index.WhenReady;
        var verifier = new DotNetVerifier(_fixture.Target, _fixture.Tests);
        Assert.True(verifier.CanBuild);
        Assert.True(verifier.CanTest);

        // Projects only in subfolders and no solution: there is no default target.
        _fixture.FileSystem.DeleteFile(TerminalFixture.PathOf("App.slnx"));

        Assert.False(verifier.CanBuild);
    }

    [Fact]
    public async Task GetErrors_BeforeAndAfterBuild_FiltersByFile()
    {
        Assert.Contains("ещё не было", await Invoke("get_errors"), StringComparison.Ordinal);

        _fixture.Runner.Returns(1, "C:\\repo\\src\\App\\A.cs(1,1): error CS1: a\nC:\\repo\\src\\App\\B.cs(2,2): error CS2: b");
        await Invoke("build");

        var errors = await Invoke("get_errors", new() { ["paths"] = new[] { "src/App/B.cs" } });
        Assert.Contains("CS2", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("CS1:", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTests_SkipsUiProjects_AndReportsFailures()
    {
        await _fixture.Index.WhenReady;
        _fixture.Runner.Returns(1, "failed App.Tests.A.B (1ms)\n  boom\nTest run summary: Failed!\n  total: 2\n  failed: 1\n  succeeded: 1\n  skipped: 0");

        var result = await Invoke("run_tests", new() { ["filter"] = "Math" });

        var request = Assert.Single(_fixture.Runner.Requests);
        Assert.Equal(["test", TerminalFixture.PathOf("tests/App.Tests/App.Tests.csproj"), "--nologo", "--filter", "FullyQualifiedName~Math"], request.Arguments);
        Assert.StartsWith("Тесты (tests/App.Tests/App.Tests.csproj): успешно 1 из 2, не прошло 1", result, StringComparison.Ordinal);
        Assert.Contains("Не прошёл App.Tests.A.B\n  boom", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunTests_UsesTestingPlatformArguments_WhenEnabledInGlobalJson()
    {
        _fixture.FileSystem.AddFile(TerminalFixture.PathOf("global.json"), """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");

        await Invoke("run_tests", new() { ["project"] = "tests/App.UI.Tests/App.UI.Tests.csproj", ["filter"] = "Startup" });

        Assert.Equal(["test", "--project", TerminalFixture.PathOf("tests/App.UI.Tests/App.UI.Tests.csproj"), "--filter-method", "*Startup*"], Assert.Single(_fixture.Runner.Requests).Arguments);
    }

    [Fact]
    public async Task Context_ShowsLastBuildAndTests()
    {
        var context = new BuildAgentContext(_fixture.Build, _fixture.Tests);
        Assert.Empty(await context.GetContextAsync(new AgentContextRequest(true), TestContext.Current.CancellationToken));

        _fixture.Runner.Returns(0, string.Empty);
        await Invoke("build");

        var line = Assert.Single(await context.GetContextAsync(new AgentContextRequest(false), TestContext.Current.CancellationToken));
        Assert.Contains("Сборка App.slnx успешна", line, StringComparison.Ordinal);
    }

    private async Task<string> Invoke(string tool, Dictionary<string, object?>? arguments = null) =>
        (await _tools[tool].InvokeAsync(new AIFunctionArguments(arguments ?? []), TestContext.Current.CancellationToken))?.ToString() ?? string.Empty;
}
