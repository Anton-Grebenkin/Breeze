using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using CodeEditor.Shell.Instances;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.App.Startup;

/// <summary>
/// The first thing a launch does (ADR 0044): looks at the other windows and either hands the request to one of them
/// and exits, or opens a window here. Runs before the host, so it builds its few services itself.
/// </summary>
internal static class StartupRouting
{
    public static IReadOnlyList<WindowEntry> OtherWindows(UserDataPaths paths) =>
        new WindowRegistry(new PhysicalFileSystem(), paths, new SystemProcessProbe(), NullLogger<WindowRegistry>.Instance).Others(Environment.ProcessId);

    /// <returns><c>null</c> if a running window took the request and this process should exit.</returns>
    public static LaunchPlan? Route(string[] args, IReadOnlyList<WindowEntry> others)
    {
        var plan = LaunchRouter.Plan(LaunchRequest.Parse(args), new PhysicalFileSystem(), others);
        if (plan.Target is not { } target)
        {
            return plan;
        }

        // No synchronization context yet: waiting on the task here can't deadlock.
        return InstanceClient.TrySendAsync(target, plan.TargetPath, AppWindows.RequestTimeout).GetAwaiter().GetResult() ? null : plan.Here();
    }
}
