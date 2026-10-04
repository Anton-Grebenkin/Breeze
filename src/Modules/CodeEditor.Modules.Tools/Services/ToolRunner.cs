using System.Globalization;
using CodeEditor.Core.Files;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Tools.Resources;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>
/// Starts a tool script in the workspace root. Parameters go as <c>-name value</c> to PowerShell, as
/// <c>--name value</c> to Node.js and C#, and to every script as <c>BREEZE_ARG_NAME</c> variables.
/// </summary>
public sealed class ToolRunner(IProcessRunner processes, IFileSystem fileSystem, IWorkspace workspace)
{
    public const string WorkspaceVariable = "BREEZE_WORKSPACE";
    public const string ArgumentPrefix = "BREEZE_ARG_";

    /// <summary>Folders to look for programs in; <c>null</c> — the PATH variable.</summary>
    public string? SearchPath { get; init; }

    /// <param name="forAgent">The agent runs it: variables that look like secrets are hidden from the script.</param>
    /// <exception cref="AgentToolException">Unknown parameter or the program for the script is not installed.</exception>
    public ProcessRequest Request(ToolDefinition tool, IReadOnlyDictionary<string, string> arguments, bool forAgent)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        var unknown = arguments.Keys.FirstOrDefault(name => !tool.Parameters.Any(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        if (unknown is not null)
        {
            throw new AgentToolException(Format(Strings.ToolUnknownParameter, unknown, tool.Name, tool.Signature.Length > 0 ? tool.Signature : "-"));
        }

        var (program, prefix, flag) = Program(tool);
        var commandArguments = new List<string>(prefix);
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WorkspaceVariable] = workspace.Root ?? string.Empty,
            ["NO_COLOR"] = "1",
        };
        foreach (var parameter in tool.Parameters)
        {
            // An empty value is not passed: the script keeps its default.
            if (Value(arguments, parameter.Name) is { Length: > 0 } value)
            {
                commandArguments.AddRange([flag + parameter.Name, value]);
                environment[ArgumentPrefix + parameter.Name.Replace('-', '_').ToUpperInvariant()] = value;
            }
        }

        return new ProcessRequest(program, commandArguments, workspace.Root ?? tool.Folder)
        {
            Timeout = tool.Timeout,
            Environment = environment,
            HideSecretVariables = forAgent,
        };
    }

    public Task<ProcessResult> RunAsync(ProcessRequest request, Action<string>? onLine, CancellationToken cancellationToken) =>
        processes.RunAsync(request, onLine, cancellationToken);

    /// <summary>Command line for the card and Output: the program name and the script relative to the root.</summary>
    public string Display(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parts = request.Arguments.Select(argument => workspace.Root is { } root && Path.IsPathRooted(argument) ? workspace.RelativePath(argument) : argument);
        return string.Join(' ', new[] { Path.GetFileNameWithoutExtension(request.FileName) }.Concat(parts).Select(Quote));
    }

    private (string Program, string[] Prefix, string Flag) Program(ToolDefinition tool) => tool.Kind switch
    {
        ToolScriptKind.PowerShell => (Find("pwsh") ?? Find("powershell") ?? throw new AgentToolException(Strings.PowerShellMissing),
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", tool.Script], "-"),
        ToolScriptKind.Node => (Find("node") ?? throw new AgentToolException(Strings.NodeMissing), [tool.Script], "--"),
        _ => (Find("dotnet") ?? throw new AgentToolException(Strings.DotnetMissing), ["run", tool.Script, "--"], "--"),
    };

    private string? Find(string name) => ExecutablePaths.Find(fileSystem, name, SearchPath).FirstOrDefault();

    private static string? Value(IReadOnlyDictionary<string, string> arguments, string name) =>
        arguments.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string Quote(string argument) => argument.Length == 0 || argument.Any(char.IsWhiteSpace) ? $"\"{argument}\"" : argument;

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
