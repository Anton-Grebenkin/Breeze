using System.Globalization;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Modules.Terminal.Services.Commands;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>
/// Long builds and test runs: past waitSeconds or on the user's click they go on in the background, the model reads
/// the report with command_output and learns of the finish from a note.
/// </summary>
public sealed class BuildJobsTests : IDisposable
{
    private const string PassedRun = "Test run summary: Passed!\n  total: 3\n  failed: 0\n  succeeded: 3\n  skipped: 0";

    private readonly TerminalFixture _fixture = new();
    private readonly RecordingVerificationLog _verification = new();
    private readonly BackgroundCommandNotes _notes;
    private readonly Dictionary<string, AIFunction> _tools;

    public BuildJobsTests()
    {
        _notes = new BackgroundCommandNotes(_fixture.Background);
        var build = new BuildAgentTools(_fixture.Build, _fixture.Tests, _fixture.Workspace, _verification, _fixture.Background, new BuildLockHints(_fixture.Background), _fixture.Activity);
        var background = new BackgroundCommandTools(_fixture.Background, _fixture.Outputs, _fixture.Activity);
        _tools = build.CreateTools().Concat(background.CreateTools()).OfType<AIFunction>().ToDictionary(tool => tool.Name);
    }

    public void Dispose()
    {
        _notes.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task LongTestRun_GoesToBackground_ThenNoteOnce_AndReportWithCommandOutput()
    {
        await _fixture.Index.WhenReady;
        var hold = _fixture.Runner.Hold = new TaskCompletionSource();
        _fixture.Runner.Returns(0, PassedRun);

        var started = await Invoke(BuildAgentTools.RunTestsName, new() { ["waitSeconds"] = 0 });

        Assert.NotNull(ResourcePattern.Match(Strings.BackgroundRunning, FirstLine(started)));
        Assert.Contains(Format(Strings.BackgroundJobHowTo, 1), started, StringComparison.Ordinal);
        Assert.Contains("  total: 3", _fixture.Activity.Lines);
        Assert.Equal(["#1 `dotnet test`"], new RunningJobs(_fixture.Background).Running());
        Assert.Empty(_verification.Records);

        hold.SetResult();
        await _fixture.Background.Get(1).WaitAsync(pattern: null, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var note = Assert.Single(await NextNotesAsync());
        var report = await Invoke(BackgroundCommandTools.CommandOutputName, new() { ["id"] = 1 });

        Assert.Contains("#1", note, StringComparison.Ordinal);
        Assert.Contains("dotnet test", note, StringComparison.Ordinal);
        Assert.Empty(_notes.TakeNotes());
        Assert.Contains("успешно 3 из 3", report, StringComparison.Ordinal);
        Assert.Equal([(VerificationKind.Tests, true)], _verification.Records);
        Assert.Empty(new RunningJobs(_fixture.Background).Running());
    }

    [Fact]
    public async Task QuickBuild_ReturnsTheReport_AsBefore()
    {
        _fixture.Runner.Returns(0, string.Empty);

        var result = await Invoke(BuildAgentTools.BuildName);

        Assert.Equal("Сборка App.slnx успешна за 2 с.", FirstLine(result));
        Assert.Empty(_fixture.Background.All);
        Assert.Empty(_notes.TakeNotes());
    }

    [Fact]
    public async Task UserMovesBuildToBackground_ToolReturns_BuildGoesOn()
    {
        var hold = _fixture.Runner.Hold = new TaskCompletionSource();
        var call = Invoke(BuildAgentTools.BuildName, new() { ["waitSeconds"] = 600 });

        _fixture.Activity.MoveToBackground();
        var result = await call;

        Assert.True(_fixture.Activity.Offered);
        Assert.NotNull(ResourcePattern.Match(Strings.BackgroundRunning, FirstLine(result)));
        Assert.Contains(Strings.MovedToBackgroundByUser, result, StringComparison.Ordinal);
        Assert.True(_fixture.Background.Get(1).IsRunning);
        hold.SetResult();
        await _fixture.Background.Get(1).WaitAsync(pattern: null, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal([(VerificationKind.Build, true)], _verification.Records);
    }

    [Fact]
    public async Task FilterMatchingNothing_SaysSo_AndIsNotAVerification()
    {
        await _fixture.Index.WhenReady;
        _fixture.Runner.Returns(8, "Test run summary: Zero tests ran - App.Tests.dll\n  total: 0\n  failed: 0\n  succeeded: 0\n  skipped: 0");

        var result = await Invoke(BuildAgentTools.RunTestsName, new() { ["filter"] = "Nothing" });

        Assert.NotNull(ResourcePattern.Match(Strings.TestsNoneMatched, FirstLine(result)));
        Assert.Empty(_verification.Records);
    }

    [Fact]
    public void LockHint_NamesTheBackgroundCommandThatStartedTheApp()
    {
        var hold = _fixture.Runner.Hold = new TaskCompletionSource();
        _fixture.Background.Start(new ProcessRequest("powershell", [], TerminalFixture.Root), "dotnet run --project src/MyApp", _ => { });

        var hint = new BuildLockHints(_fixture.Background).For(MsBuildOutputParser.Parse(Locked("MyApp (46980)")));

        Assert.Equal("\n\n" + Format(Strings.BuildLockedByBackground, "MyApp (46980)", 1, "dotnet run --project src/MyApp"), hint);
        hold.SetResult();
    }

    [Fact]
    public void LockHint_WithoutItsCommand_SaysTheAppIsRunning()
    {
        var hint = new BuildLockHints(_fixture.Background).For(MsBuildOutputParser.Parse(Locked("Other.Host (7), dotnet (8)")));

        Assert.Equal("\n\n" + Format(Strings.BuildLocked, "Other.Host (7), dotnet (8)"), hint);
        Assert.Empty(new BuildLockHints(_fixture.Background).For(MsBuildOutputParser.Parse(@"C:\repo\src\App\A.cs(1,1): error CS1: a")));
    }

    private static string Locked(string processes) =>
        @"C:\sdk\Microsoft.Common.CurrentVersion.targets(5034,5): error MSB3027: Could not copy ""obj\Debug\MyApp.dll"" to ""bin\Debug\MyApp.dll"". Exceeded retry count of 10. Failed. The file is locked by: """ + processes + @""" [C:\repo\src\MyApp\MyApp.csproj]";

    private async Task<string> Invoke(string name, Dictionary<string, object?>? arguments = null) =>
        (await _tools[name].InvokeAsync(new AIFunctionArguments(arguments ?? []), TestContext.Current.CancellationToken))!.ToString()!;

    // The note comes right after the finish, on the job's thread.
    private async Task<IReadOnlyList<string>> NextNotesAsync()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (_notes.TakeNotes() is { Count: > 0 } notes)
            {
                return notes;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return [];
    }

    private static string FirstLine(string text) => text.Split('\n')[0];

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
