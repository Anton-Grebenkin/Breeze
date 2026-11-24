using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Tests.Infrastructure;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Agent.Tests.Tools;

/// <summary>
/// Long tool output (ADR 0012): the model gets the head and tail, the full text goes to the agent data folder.
/// </summary>
public sealed class ToolOutputTests : IDisposable
{
    private static readonly string Outputs = Path.Combine(AgentFixture.Root, ".breeze", "agent", "outputs");

    private readonly AgentFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void HeadAndTail_KeepsBothEnds_OnLineBoundaries()
    {
        var text = Lines(1_000);

        var preview = ToolOutput.HeadAndTail(text, 100, 200);

        Assert.StartsWith("line 1\n", preview, StringComparison.Ordinal);
        Assert.EndsWith("line 1000", preview, StringComparison.Ordinal);
        Assert.True(preview.Length < 400);
        Assert.All(preview.Split('\n'), line => Assert.Matches(@"^(line \d+|… пропущено строк: \d+ …)$", line));
        var shown = preview.Split('\n').Count(line => line.StartsWith("line", StringComparison.Ordinal));
        Assert.Contains($"пропущено строк: {1_000 - shown}", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadAndTail_ShortText_IsUnchanged() => Assert.Equal("a\nb", ToolOutput.HeadAndTail("a\nb", 10, 10));

    // One huge line between short ones (an API answer after the exit code line) is cut by characters, not dropped.
    [Fact]
    public void HeadAndTail_HugeLine_IsCutByCharacters()
    {
        var text = "Код выхода 0\n" + new string('a', 10_000) + new string('b', 10_000) + "\nготово";

        var preview = ToolOutput.HeadAndTail(text, 400, 600);

        Assert.StartsWith("Код выхода 0\naaaa", preview, StringComparison.Ordinal);
        Assert.EndsWith("bbbb\nготово", preview, StringComparison.Ordinal);
        Assert.Contains($"\n… пропущено знаков: {text.Length - 1_000} …\n", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void Limit_HugeLine_KeepsTheAllowedLength() =>
        Assert.StartsWith("ok\n" + new string('a', 97) + "\n…(обрезано: показано 100 из", ToolOutput.Limit("ok\n" + new string('a', 500), 100), StringComparison.Ordinal);

    // JSON in one line is saved indented: the head shows its structure and read_file pages it by lines.
    [Fact]
    public void LongJsonLine_IsSavedIndented()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        var json = "{\"items\":[" + string.Join(',', Enumerable.Range(1, 2_000).Select(id => $"{{\"id\":{id},\"name\":\"запись {id}\"}}")) + "]}";

        var result = _fixture.Outputs.Fit("Код выхода 0\n" + json + "\n", "run_command");

        var saved = _fixture.FileSystem.ReadAllText(Assert.Single(_fixture.FileSystem.EnumerateEntries(Outputs)).FullPath);
        Assert.StartsWith("Код выхода 0\n{\n  \"items\": [\n    {\n      \"id\": 1,\n      \"name\": \"запись 1\"\n    },", saved, StringComparison.Ordinal);
        Assert.StartsWith("Код выхода 0\n{\n  \"items\": [", result, StringComparison.Ordinal);
        Assert.Contains("пропущено строк:", result, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"запись 2000\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void LongLineThatIsNotJson_StaysAsItIs()
    {
        var text = "{ not json " + new string('x', 30_000);

        Assert.Equal(text, JsonLines.Unfold(text));
    }

    [Fact]
    public void ShortOutput_GoesAsIs() => Assert.Equal("коротко", _fixture.Outputs.Fit("коротко", "run_command"));

    [Fact]
    public void LongOutput_IsSavedToAgentFolder_ModelGetsEndsAndPath()
    {
        _fixture.Workspace.Open(AgentFixture.Root);
        var text = Lines(5_000);

        var result = _fixture.Outputs.Fit(text, "run_command");

        var saved = Assert.Single(_fixture.FileSystem.EnumerateEntries(Outputs));
        Assert.EndsWith("-run_command.txt", saved.Name, StringComparison.Ordinal);
        Assert.Equal(text, _fixture.FileSystem.ReadAllText(saved.FullPath));
        Assert.True(_fixture.FileSystem.FileExists(Path.Combine(AgentFixture.Root, ".breeze", "agent", ".gitignore")));
        Assert.StartsWith("line 1\n", result, StringComparison.Ordinal);
        Assert.Contains("line 5000", result, StringComparison.Ordinal);
        Assert.Contains($".breeze/agent/outputs/{saved.Name}", result, StringComparison.Ordinal);
        Assert.Contains("строк 5000", result, StringComparison.Ordinal);
        Assert.True(result.Length < AgentOutputStore.HeadCharacters + AgentOutputStore.TailCharacters + 1_000);
    }

    [Fact]
    public void OutputsOlderThanThreeDays_AreRemoved_OnNextSave()
    {
        var time = new ManualTimeProvider();
        var store = new AgentOutputStore(_fixture.Workspace, _fixture.FileSystem, time, NullLogger<AgentOutputStore>.Instance);
        _fixture.Workspace.Open(AgentFixture.Root);
        var stale = Path.Combine(Outputs, time.GetLocalNow().AddDays(-4).ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + "-1-run_command.txt");
        var fresh = Path.Combine(Outputs, time.GetLocalNow().AddDays(-1).ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + "-2-run_command.txt");
        _fixture.FileSystem.AddFile(stale, "старое").AddFile(fresh, "вчерашнее").AddFile(Path.Combine(Outputs, "notes.txt"), "чужое");

        store.Fit(Lines(5_000), "run_command");

        Assert.False(_fixture.FileSystem.FileExists(stale));
        Assert.True(_fixture.FileSystem.FileExists(fresh));
        Assert.True(_fixture.FileSystem.FileExists(Path.Combine(Outputs, "notes.txt")));
    }

    [Fact]
    public void WithoutFolder_OnlyEndsAreKept() =>
        Assert.Contains("Сузь команду", _fixture.Outputs.Fit(Lines(5_000), "run_command"), StringComparison.Ordinal);

    [Fact]
    public void CollapsedToolResults_ForgetReadWindows_ButKeepVersions()
    {
        var path = Path.Combine(AgentFixture.Root, "a.cs");
        _fixture.FileState.RecordRead(path, "class A { }", 1, 1);
        Assert.True(_fixture.FileState.RecordRead(path, "class A { }", 1, 1));

        _fixture.FileState.ForgetWindows();

        Assert.False(_fixture.FileState.RecordRead(path, "class A { }", 1, 1));
        Assert.Null(_fixture.FileState.CheckEditable(path, "a.cs", "class A { }"));
    }

    private static string Lines(int count) => string.Join('\n', Enumerable.Range(1, count).Select(line => $"line {line}"));
}
