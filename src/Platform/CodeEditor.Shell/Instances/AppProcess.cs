using System.Diagnostics;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// Starts another process of the app: a new window or the restart. Environment variables (data folder, language) are
/// inherited. Under <c>dotnet Breeze.dll</c> the assembly path goes first.
/// </summary>
public static class AppProcess
{
    private const string DotNetHost = "dotnet";

    public static void Start(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unknown");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), DotNetHost, StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start);
    }
}
