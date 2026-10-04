using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Output;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// <c>dotnet test</c> for the Run Tests command and the agent's <c>run_tests</c> tool. Without a project, runs all
/// test projects except UI tests (they open windows and drive the mouse). The name filter uses the runner's syntax:
/// Microsoft.Testing.Platform if enabled in <c>global.json</c>, otherwise VSTest.
/// </summary>
public sealed partial class DotNetTests(
    IProcessRunner runner,
    DotNetTarget target,
    IWorkspace workspace,
    IFileIndex index,
    IFileSystem fileSystem,
    IOutputService output,
    SaveBeforeRun saveBeforeRun,
    ILogger<DotNetTests> logger)
{
    public static string ChannelName => Strings.Tests;

    private const string TestingPlatformMarker = "Microsoft.Testing.Platform";

    /// <summary>The workspace has unit test projects (UI tests don't count).</summary>
    public bool HasUnitTests => UnitTestProjects().Any();

    /// <summary>The last test run in this session; <c>null</c> if none yet.</summary>
    public TestReport? Last { get; private set; }

    /// <param name="project">Test project; <c>null</c> runs all except UI tests.</param>
    /// <param name="filter">Part of a test's full name (class or method).</param>
    public async Task<TestReport> RunAsync(string? project, string? filter, CancellationToken cancellationToken)
    {
        var projects = Projects(project);
        await saveBeforeRun.SaveAsync(cancellationToken);
        var channel = output.GetOrCreate(ChannelName);
        channel.Clear();
        var reports = new List<TestReport>();
        var elapsed = TimeSpan.Zero;
        foreach (var path in projects)
        {
            var relative = workspace.RelativePath(path);
            channel.AppendLine($"> dotnet test {relative}{(filter is null ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.TestFilter, filter))}");
            var result = await runner.RunAsync(new ProcessRequest(DotNetEnvironment.Executable, Arguments(path, filter), workspace.Root!)
            {
                Environment = DotNetEnvironment.Variables,
            }, channel.AppendLine, cancellationToken);
            elapsed += result.Elapsed;
            reports.Add(TestOutputParser.Parse(relative, result.Output) with { TimedOut = result.TimedOut, OutputTail = result.Output });
        }

        var report = Combine(reports, elapsed);
        channel.AppendLine(TestReportText.Summary(report));
        LogTested(logger, report.Target, report.Total, report.Failed, (long)elapsed.TotalMilliseconds);
        Last = report;
        return report;
    }

    private IReadOnlyList<string> Projects(string? project)
    {
        if (!string.IsNullOrWhiteSpace(project))
        {
            var path = target.Resolve(project);
            return DotNetTarget.IsProject(path) ? [path] : TestProjects();
        }

        return TestProjects();
    }

    private List<string> TestProjects()
    {
        var root = workspace.Root!;
        var projects = UnitTestProjects()
            .Select(file => Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return projects.Count > 0 ? projects : [target.Resolve(null)];
    }

    private IEnumerable<IndexedFile> UnitTestProjects() =>
        index.Files.Where(file => DotNetTarget.IsProject(file.RelativePath) && IsTestProject(file.RelativePath) && !IsUiTestProject(file.RelativePath));

    private List<string> Arguments(string project, string? filter)
    {
        var testingPlatform = UsesTestingPlatform();
        List<string> arguments = testingPlatform ? ["test", "--project", project] : ["test", project, "--nologo"];
        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments.AddRange(testingPlatform ? ["--filter-method", $"*{filter}*"] : ["--filter", $"FullyQualifiedName~{filter}"]);
        }

        return arguments;
    }

    private bool UsesTestingPlatform()
    {
        var globalJson = Path.Combine(workspace.Root!, "global.json");
        return fileSystem.FileExists(globalJson) && fileSystem.ReadAllText(globalJson).Contains(TestingPlatformMarker, StringComparison.Ordinal);
    }

    // "…Tests" and "…Test", but not "…Testing": those are test helper libraries, not tests.
    private static bool IsTestProject(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUiTestProject(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Contains(".UI.", StringComparison.OrdinalIgnoreCase) || name.Contains("UITest", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".UI.Tests", StringComparison.OrdinalIgnoreCase);
    }

    private static TestReport Combine(List<TestReport> reports, TimeSpan elapsed) =>
        new(
            string.Join(", ", reports.Select(report => report.Target)),
            reports.Sum(report => report.Total),
            reports.Sum(report => report.Passed),
            reports.Sum(report => report.Failed),
            reports.Sum(report => report.Skipped),
            [.. reports.SelectMany(report => report.Failures)],
            [.. reports.SelectMany(report => report.BuildErrors)],
            reports.All(report => report.Parsed))
        {
            Elapsed = elapsed,
            TimedOut = reports.Any(report => report.TimedOut),
            OutputTail = reports.FirstOrDefault(report => !report.Succeeded)?.OutputTail ?? string.Empty,
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "Tests {Target}: total {Total}, failed {Failed}, {ElapsedMs} ms")]
    private static partial void LogTested(ILogger logger, string target, int total, int failed, long elapsedMs);
}
