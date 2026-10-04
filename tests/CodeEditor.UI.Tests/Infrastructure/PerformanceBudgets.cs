namespace CodeEditor.UI.Tests.Infrastructure;

/// <summary>
/// Performance budgets from CONTRIBUTING.md. The startup budget is for Release; Debug has no optimizations, so its checks
/// are looser.
/// </summary>
internal static class PerformanceBudgets
{
    public const long Megabyte = 1024 * 1024;

    public const long IdlePrivateMemoryBytes = 150 * Megabyte;

    /// <summary>Managed heap growth over 100 tab open/close cycles: under 40 KB per cycle isn't a leak.</summary>
    public const long TabCyclesHeapGrowthKilobytes = 4 * 1024;

    /// <summary>UI Automation lag between an action and the test observing it.</summary>
    public static readonly TimeSpan AutomationOverhead = TimeSpan.FromMilliseconds(700);

#if DEBUG
    public static readonly TimeSpan ColdStart = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan LargeFileOpen = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan UiThreadBlock = TimeSpan.FromMilliseconds(500);
#else
    public static readonly TimeSpan ColdStart = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LargeFileOpen = TimeSpan.FromMilliseconds(300);

    /// <summary>How long creating a large file's buffer may block the UI thread: about six frames.</summary>
    public static readonly TimeSpan UiThreadBlock = TimeSpan.FromMilliseconds(100);
#endif
}
