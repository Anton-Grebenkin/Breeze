using System.Diagnostics;

namespace CodeEditor.App.Startup;

/// <summary>
/// Measures startup time: from process start, and by phase from entering <c>Main</c>.
/// </summary>
internal sealed class StartupClock
{
    private readonly long _mainTimestamp = Stopwatch.GetTimestamp();
    private readonly List<(string Phase, TimeSpan Elapsed)> _phases = [];

    /// <summary>Time since entering <c>Main</c>, excluding runtime load.</summary>
    public TimeSpan SinceMain => Stopwatch.GetElapsedTime(_mainTimestamp);

    /// <summary>Startup phases with time since entering <c>Main</c>.</summary>
    public IReadOnlyList<(string Phase, TimeSpan Elapsed)> Phases => _phases;

    /// <summary>Time since process start: what the user perceives.</summary>
    public static TimeSpan SinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }

    public void Mark(string phase) => _phases.Add((phase, SinceMain));
}
