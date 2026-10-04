using System.Diagnostics;
using System.Globalization;
using CodeEditor.Core.Commands;
using CodeEditor.Shell.Resources;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.Shell.Commands;

/// <summary>
/// Developer commands, palette only (like "Developer: …" in VS Code). "Show memory" collects garbage and reports the
/// managed heap and working set; the leak UI test relies on it.
/// </summary>
public sealed class DeveloperCommands(StatusBarViewModel statusBar) : IDisposable
{
    public const string ShowMemoryId = "developer.showMemory";

    private const long Kilobyte = 1024;
    private const long Megabyte = 1024 * 1024;

    private IDisposable? _registration;

    public void Register(ICommandRegistry commands) =>
        _registration = commands.Register(new CommandDefinition(ShowMemoryId, Strings.ShowMemory, (_, _) =>
        {
            statusBar.Message = MeasureMemory();
            return ValueTask.CompletedTask;
        }, Strings.CategoryDeveloper));

    public void Dispose() => _registration?.Dispose();

    /// <summary>Runs a full collection with finalizers first, so only reachable objects are counted.</summary>
    public static string MeasureMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        var heap = GC.GetTotalMemory(forceFullCollection: false) / Kilobyte;
        var workingSet = process.WorkingSet64 / Megabyte;
        return string.Format(CultureInfo.InvariantCulture, Strings.MemoryStatus, heap, workingSet);
    }
}
