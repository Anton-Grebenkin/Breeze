using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Turn;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Commands;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Agent verification tools, an external check of its work (ADR 0008): <c>build</c>, <c>run_tests</c>,
/// <c>get_errors</c>. No approval: builds and tests are local and don't change sources. Results go to the agent's
/// verification gate (<see cref="IAgentVerificationLog"/>), whose hint is appended to the result.
/// A build or test run is awaited for at most <c>waitSeconds</c>, like <c>run_command</c>; a longer one, or one the
/// user moved to the background from the feed, goes on as a background job (<see cref="BackgroundCommands"/>): a slow
/// suite doesn't hold the turn, and the model reads the report with <c>command_output</c>.
/// </summary>
public sealed class BuildAgentTools(
    DotNetBuild build,
    DotNetTests tests,
    IWorkspace workspace,
    IAgentVerificationLog verification,
    BackgroundCommands background,
    BuildLockHints lockHints,
    IAgentToolActivity activity) : IAgentToolProvider
{
    public const string BuildName = "build";
    public const string RunTestsName = "run_tests";
    public const string GetErrorsName = "get_errors";

    /// <summary>Default wait for a build or test run before it goes to the background.</summary>
    public const int DefaultWaitSeconds = 120;

    public const int MaxWaitSeconds = 600;

    private const string InBackground =
        " Waits up to waitSeconds (default 120); a longer run goes on in the background: you get its id, command_output(id) returns the report once it is done, and you get a note when it finishes.";

    private const string WaitDescription = "How long to wait for the result, 0–600 seconds; default 120. If the run takes longer, it goes on in the background.";

    public IEnumerable<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(BuildAsync, BuildName,
            "Builds the solution (or a given project) with dotnet build and returns errors and warnings as 'path(line,col): error CODE: message'. Run it after code edits: a task with edits is not done while the build fails." + InBackground),
        AIFunctionFactory.Create(RunTestsAsync, RunTestsName,
            "Runs unit tests with dotnet test and returns totals and only the failed tests with their messages. Without project — all test projects except UI tests. Use filter (part of a test class or method name; several joined with |) to run only the relevant tests first." + InBackground),
        new ReadOnlyAIFunction(AIFunctionFactory.Create(GetErrors, GetErrorsName,
            "Returns errors and warnings of the last build in this session, optionally only for given files. Does not build; call build after edits.")),
    ];

    private Task<string> BuildAsync(
        [Description("Solution or project path relative to the workspace root; default — the solution in the root.")] string? project = null,
        [Description(WaitDescription)] int waitSeconds = DefaultWaitSeconds,
        CancellationToken cancellationToken = default) =>
        RunAsync(Title("dotnet build", project), waitSeconds, async (onLine, token) =>
        {
            var started = verification.StartCheck();
            var report = await build.BuildAsync(project, onLine, token);
            var hint = verification.Record(VerificationKind.Build, report.Succeeded && !report.TimedOut, started);
            return new JobResult(report.Succeeded ? 0 : 1, BuildReportText.Details(report, workspace.RelativePath) + lockHints.For(report.Diagnostics) + hint);
        }, cancellationToken);

    private Task<string> RunTestsAsync(
        [Description("Test project path relative to the workspace root; default — all test projects except UI tests.")] string? project = null,
        [Description("Part of a test class or method name to run only matching tests; several parts joined with |.")] string? filter = null,
        [Description(WaitDescription)] int waitSeconds = DefaultWaitSeconds,
        CancellationToken cancellationToken = default) =>
        RunAsync(Title("dotnet test", project) + (filter is null ? string.Empty : Format(Strings.TestFilter, filter)), waitSeconds, async (onLine, token) =>
        {
            var started = verification.StartCheck();
            var report = await tests.RunAsync(project, filter, onLine, token);

            // A filter that matched nothing checked nothing: neither a pass nor a failure for the gate.
            var hint = report.NoTests ? null : verification.Record(VerificationKind.Tests, report.Succeeded, started);
            return new JobResult(report.Succeeded ? 0 : 1, TestReportText.Details(report, workspace.RelativePath) + lockHints.For(report.BuildErrors) + hint);
        }, cancellationToken);

    // Waits for the job as run_command waits for its command. The job's lines go to the feed row of this call.
    private async Task<string> RunAsync(string title, int waitSeconds, Func<Action<string>, CancellationToken, Task<JobResult>> run, CancellationToken cancellationToken)
    {
        var call = activity.Current();
        var release = call.OfferBackground();
        var job = background.StartJob(title, run, call.Report);
        try
        {
            await job.WaitAsync(pattern: null, TimeSpan.FromSeconds(Math.Clamp(waitSeconds, 0, MaxWaitSeconds)), cancellationToken, release);
        }
        catch (OperationCanceledException)
        {
            // The turn was cancelled: stop this step's run with it.
            background.Stop(job.Id);
            throw;
        }

        if (job.TryDetach())
        {
            return background.Report(job, string.Empty, release.IsCancellationRequested);
        }

        background.Forget(job.Id);
        var report = job.ReadNew().TrimEnd();
        if (job.ExitCode is null)
        {
            // The run failed to start (the message says why) or was stopped with the workspace.
            throw new AgentToolException(report.Length > 0 ? report : background.Report(job, string.Empty));
        }

        return report;
    }

    private string GetErrors([Description("Files relative to the workspace root; empty — all.")] string[]? paths = null)
    {
        if (build.Last is not { } report)
        {
            return Strings.NoBuildYet;
        }

        var wanted = (paths ?? []).Select(path => WorkspacePaths.Resolve(workspace, path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var diagnostics = report.Diagnostics
            .Where(diagnostic => wanted.Count == 0 || (diagnostic.File is { } file && wanted.Contains(file)))
            .Select(diagnostic => diagnostic.Format(workspace.RelativePath))
            .Take(BuildReportText.MaxDiagnostics)
            .ToList();
        var header = Format(Strings.ErrorsHeader, report.Target, report.Finished);
        return diagnostics.Count == 0 ? header + Strings.NoDiagnostics : header + "\n" + string.Join('\n', diagnostics);
    }

    private static string Title(string command, string? project) => string.IsNullOrWhiteSpace(project) ? command : command + " " + project;

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
