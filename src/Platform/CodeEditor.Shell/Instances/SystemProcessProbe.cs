using System.ComponentModel;
using System.Diagnostics;

namespace CodeEditor.Shell.Instances;

public sealed class SystemProcessProbe : IProcessProbe
{
    public DateTime? StartTimeOf(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.StartTime.ToUniversalTime();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    /// <summary>The start time of this process, as <see cref="StartTimeOf"/> reports it to others.</summary>
    public static DateTime CurrentStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime.ToUniversalTime();
    }
}
