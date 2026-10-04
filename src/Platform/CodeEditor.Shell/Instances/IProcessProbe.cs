namespace CodeEditor.Shell.Instances;

/// <summary>Whether a process still runs: the window registry drops entries of exited processes.</summary>
public interface IProcessProbe
{
    /// <summary>The start time (UTC) of a running process; <c>null</c> if it has exited.</summary>
    DateTime? StartTimeOf(int processId);
}
