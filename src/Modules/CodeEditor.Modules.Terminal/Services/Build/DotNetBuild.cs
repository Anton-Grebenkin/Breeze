using CodeEditor.Core.Files;
using CodeEditor.Core.Output;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Terminal.Resources;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// <c>dotnet build</c> for the Build command (<c>Ctrl+Shift+B</c>) and the agent's <c>build</c> tool: output goes to
/// the Build channel, errors are parsed, the last result is kept (<c>get_errors</c>, agent context).
/// One build at a time: parallel builds would fight over files in obj.
/// </summary>
public sealed partial class DotNetBuild(
    IProcessRunner runner,
    DotNetTarget target,
    IWorkspace workspace,
    IOutputService output,
    SaveBeforeRun saveBeforeRun,
    TimeProvider time,
    ILogger<DotNetBuild> logger) : IDisposable
{
    public static string ChannelName => Strings.Build;

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The last build in this session; <c>null</c> if none yet.</summary>
    public BuildReport? Last { get; private set; }

    /// <summary>Raised on a background thread when a build finishes.</summary>
    public event EventHandler? Completed;

    public async Task<BuildReport> BuildAsync(string? project, CancellationToken cancellationToken)
    {
        var path = target.Resolve(project);
        var relative = workspace.RelativePath(path);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await saveBeforeRun.SaveAsync(cancellationToken);
            var channel = output.GetOrCreate(ChannelName);
            channel.Clear();
            channel.AppendLine($"> dotnet build {relative}");
            var result = await runner.RunAsync(
                new ProcessRequest(DotNetEnvironment.Executable, ["build", path, "-nologo", "-v:q", "-clp:NoSummary", "-p:GenerateFullPaths=true"], workspace.Root!)
                {
                    Environment = DotNetEnvironment.Variables,
                },
                channel.AppendLine,
                cancellationToken);

            var report = new BuildReport(relative, result.ExitCode == 0, MsBuildOutputParser.Parse(result.Output), result.Elapsed, time.GetLocalNow())
            {
                OutputTail = result.Output,
                TimedOut = result.TimedOut,
            };
            channel.AppendLine(BuildReportText.Summary(report));
            LogBuilt(logger, relative, report.Succeeded, report.Errors, report.Warnings, (long)report.Elapsed.TotalMilliseconds);
            Last = report;
            Completed?.Invoke(this, EventArgs.Empty);
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    [LoggerMessage(Level = LogLevel.Information, Message = "Build {Target}: succeeded {Succeeded}, errors {Errors}, warnings {Warnings}, {ElapsedMs} ms")]
    private static partial void LogBuilt(ILogger logger, string target, bool succeeded, int errors, int warnings, long elapsedMs);
}
