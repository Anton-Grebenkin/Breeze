using System.ComponentModel;
using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Files;
using CodeEditor.Modules.Agent.Contracts.Verification;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Agent verification tools, an external check of its work (ADR 0008): <c>build</c>, <c>run_tests</c>,
/// <c>get_errors</c>. No approval: builds and tests are local and don't change sources. Results go to the agent's
/// verification gate (<see cref="IAgentVerificationLog"/>), whose hint is appended to the result.
/// </summary>
public sealed class BuildAgentTools(DotNetBuild build, DotNetTests tests, IWorkspace workspace, IAgentVerificationLog verification) : IAgentToolProvider
{
    public const string BuildName = "build";
    public const string RunTestsName = "run_tests";
    public const string GetErrorsName = "get_errors";

    public IEnumerable<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(BuildAsync, BuildName,
            "Builds the solution (or a given project) with dotnet build and returns errors and warnings as 'path(line,col): error CODE: message'. Run it after code edits: a task with edits is not done while the build fails."),
        AIFunctionFactory.Create(RunTestsAsync, RunTestsName,
            "Runs unit tests with dotnet test and returns totals and only the failed tests with their messages. Without project — all test projects except UI tests. Use filter (part of a test class or method name) to run only the relevant tests first."),
        new ReadOnlyAIFunction(AIFunctionFactory.Create(GetErrors, GetErrorsName,
            "Returns errors and warnings of the last build in this session, optionally only for given files. Does not build; call build after edits.")),
    ];

    private async Task<string> BuildAsync(
        [Description("Solution or project path relative to the workspace root; default — the solution in the root.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var report = await build.BuildAsync(project, cancellationToken);
        return BuildReportText.Details(report, workspace.RelativePath) + verification.Record(VerificationKind.Build, report.Succeeded && !report.TimedOut);
    }

    private async Task<string> RunTestsAsync(
        [Description("Test project path relative to the workspace root; default — all test projects except UI tests.")] string? project = null,
        [Description("Part of a test class or method name to run only matching tests.")] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var report = await tests.RunAsync(project, filter, cancellationToken);
        return TestReportText.Details(report, workspace.RelativePath) + verification.Record(VerificationKind.Tests, report.Succeeded);
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
        var header = string.Format(CultureInfo.CurrentCulture, Strings.ErrorsHeader, report.Target, report.Finished);
        return diagnostics.Count == 0 ? header + Strings.NoDiagnostics : header + "\n" + string.Join('\n', diagnostics);
    }
}
