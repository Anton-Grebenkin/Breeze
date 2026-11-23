using System.Globalization;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Build;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Runs an agent command in Windows PowerShell 5.1 (ADR 0012, 0052). The command goes on the command line as plain
/// script text: running text taken from an environment variable through <c>[ScriptBlock]::Create</c> is how malware hides
/// code, and corporate antivirus blocked every command for it. <see cref="PowerShellWrapping.Full"/> adds a prologue
/// that enables UTF-8 (otherwise non-ASCII output is garbled and <c>&gt;</c> writes UTF-16) and an epilogue that returns
/// the native exit code (otherwise a failed <c>dotnet build</c> exits with 0). The command sits on its own lines inside
/// a script block, so a trailing comment cannot swallow the epilogue and <c>return</c> keeps its meaning. The
/// environment has no pager, colors or prompts and English .NET messages; variables holding keys and tokens are
/// removed (<see cref="ProcessRequest.HideSecretVariables"/>).
/// </summary>
public static class PowerShellLauncher
{
    public const string Shell = "powershell.exe";

    /// <summary>Windows limits a command line to 32 767 characters; the rest is left for the program and switches.</summary>
    public const int MaxScriptLength = 30_000;

    /// <summary>PowerShell's error id for a script its antimalware scan (AMSI) refused to run.</summary>
    public const string BlockedMarker = "ScriptContainedMaliciousContent";

    private const string Prologue =
        "$u = New-Object System.Text.UTF8Encoding $false; [Console]::OutputEncoding = $u; $OutputEncoding = $u; " +
        "$PSDefaultParameterValues['*:Encoding'] = 'utf8'; $ProgressPreference = 'SilentlyContinue'; $global:LASTEXITCODE = 0";

    private const string Epilogue = "if ($global:LASTEXITCODE) { exit $global:LASTEXITCODE } elseif (-not $?) { exit 1 }";

    private static readonly string[] Switches = ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command"];

    private static readonly Dictionary<string, string> QuietVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NO_COLOR"] = "1",
        ["TERM"] = "dumb",
        ["PAGER"] = "cat",
        ["GIT_PAGER"] = "cat",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_EDITOR"] = ":",
        ["GIT_SEQUENCE_EDITOR"] = ":",
    };

    /// <summary>The script text for <c>-Command</c>; <c>&amp;&amp;</c> and <c>||</c> chains are rewritten.</summary>
    public static string Script(string command, PowerShellWrapping wrapping = PowerShellWrapping.Full)
    {
        ArgumentNullException.ThrowIfNull(command);
        var rewritten = PowerShellChains.Rewrite(command);
        return wrapping == PowerShellWrapping.Bare ? rewritten : Prologue + "\n& {\n" + rewritten + "\n}\n" + Epilogue;
    }

    /// <param name="command">The command as sent by the model.</param>
    /// <exception cref="AgentToolException">The script does not fit on a command line.</exception>
    public static ProcessRequest Request(string command, string folder, TimeSpan timeout, PowerShellWrapping wrapping = PowerShellWrapping.Full)
    {
        var script = Script(command, wrapping);
        if (script.Length > MaxScriptLength)
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.CommandTooLong, script.Length, MaxScriptLength));
        }

        var environment = new Dictionary<string, string>(QuietVariables, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in DotNetEnvironment.Variables)
        {
            environment[name] = value;
        }

        return new ProcessRequest(Shell, [.. Switches, script], folder)
        {
            Timeout = timeout,
            Environment = environment,
            HideSecretVariables = true,
        };
    }

    /// <summary>The antimalware scan refused the script, so nothing in it ran.</summary>
    public static bool IsBlocked(ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.ExitCode != 0 && result.Output.Contains(BlockedMarker, StringComparison.Ordinal);
    }
}
