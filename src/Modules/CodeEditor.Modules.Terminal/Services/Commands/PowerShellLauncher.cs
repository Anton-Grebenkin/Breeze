using CodeEditor.Core.Processes;
using CodeEditor.Modules.Terminal.Services.Build;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Runs an agent command in Windows PowerShell 5.1 (ADR 0012). The command is passed in an environment variable and
/// run via <c>[ScriptBlock]::Create</c> after a prologue that enables UTF-8: without it non-ASCII output and parse
/// errors come out garbled and <c>&gt;</c> writes UTF-16. The epilogue returns the native exit code: without it a
/// failed <c>dotnet build</c> exits with 0. The environment has no pager, colors or prompts and English .NET messages;
/// variables holding keys and tokens are removed (<see cref="ProcessRequest.HideSecretVariables"/>).
/// </summary>
public static class PowerShellLauncher
{
    public const string Shell = "powershell.exe";

    /// <summary>Environment variable holding the command text.</summary>
    public const string CommandVariable = "CODEEDITOR_AGENT_COMMAND";

    /// <summary>Prologue, command and epilogue as one <c>-Command</c> string.</summary>
    public const string Script =
        "$u = New-Object System.Text.UTF8Encoding $false; [Console]::OutputEncoding = $u; $OutputEncoding = $u; " +
        "$PSDefaultParameterValues['*:Encoding'] = 'utf8'; $ProgressPreference = 'SilentlyContinue'; $global:LASTEXITCODE = 0; " +
        "& ([ScriptBlock]::Create($env:" + CommandVariable + ")); " +
        "if ($global:LASTEXITCODE) { exit $global:LASTEXITCODE } elseif (-not $?) { exit 1 }";

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

    public static IReadOnlyList<string> Arguments { get; } = ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", Script];

    /// <param name="command">The command as sent by the model; <c>&amp;&amp;</c> and <c>||</c> chains are rewritten.</param>
    public static ProcessRequest Request(string command, string folder, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(command);
        var environment = new Dictionary<string, string>(QuietVariables, StringComparer.OrdinalIgnoreCase)
        {
            [CommandVariable] = PowerShellChains.Rewrite(command),
        };
        foreach (var (name, value) in DotNetEnvironment.Variables)
        {
            environment[name] = value;
        }

        return new ProcessRequest(Shell, Arguments, folder)
        {
            Timeout = timeout,
            Environment = environment,
            HideSecretVariables = true,
        };
    }
}
