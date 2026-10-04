using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using CodeEditor.Shell.ViewModels;

namespace CodeEditor.App.Diagnostics;

/// <summary>Log file header: version, runtime and process, so a log from someone else's report can be matched up.</summary>
internal static class SessionInfo
{
    public static string Version { get; } =
        typeof(SessionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(SessionInfo).Assembly.GetName().Version?.ToString()
        ?? "?";

    public static string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"{MainWindowViewModel.ProductName} {Version} · .NET {Environment.Version} · {RuntimeInformation.OSDescription} {RuntimeInformation.ProcessArchitecture} · process {Environment.ProcessId}");
}
