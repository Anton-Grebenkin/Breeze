using System.Globalization;

namespace CodeEditor.Shell.Instances;

/// <summary>An open Breeze window: one process per window (ADR 0044). Other processes send it requests by pipe.</summary>
/// <param name="StartTime">Process start time (UTC): a process that reused the id is not taken for the window.</param>
/// <param name="Folder">The open folder; <c>null</c> for an empty window.</param>
/// <param name="LastActive">When the window was last activated: a file without its own window goes to the latest.</param>
public sealed record WindowEntry(int ProcessId, DateTime StartTime, string? Folder, DateTimeOffset LastActive)
{
    private const string PipePrefix = "Breeze.Window.";

    public string PipeName => PipeNameOf(ProcessId);

    public static string PipeNameOf(int processId) => PipePrefix + processId.ToString(CultureInfo.InvariantCulture);
}
