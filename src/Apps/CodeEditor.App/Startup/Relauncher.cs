using System.Diagnostics;
using System.IO;

namespace CodeEditor.App.Startup;

/// <summary>
/// Starts a new instance after exit (the Restart command). No arguments: the session restores folder and tabs;
/// environment variables (data folder, language) are inherited. Under <c>dotnet CodeEditor.App.dll</c> the assembly
/// path is passed too.
/// </summary>
internal static class Relauncher
{
    private const string DotNetHost = "dotnet";

    public static void StartNewInstance()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unknown");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), DotNetHost, StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        using var process = Process.Start(start);
    }
}
