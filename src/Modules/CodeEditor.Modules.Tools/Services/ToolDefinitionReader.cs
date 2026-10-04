using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Text;
using CodeEditor.Modules.Tools.Resources;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>Reads a tool folder: the <c>tool.md</c> header and the script next to it.</summary>
public static partial class ToolDefinitionReader
{
    public const int MaxTimeoutSeconds = 600;

    private const string DescriptionKey = "description";
    private const string ParametersKey = "parameters";
    private const string RunKey = "run";
    private const string TimeoutKey = "timeout";

    private static readonly FrozenSet<string> Keys = FrozenSet.ToFrozenSet([DescriptionKey, ParametersKey, RunKey, TimeoutKey], StringComparer.OrdinalIgnoreCase);

    /// <summary>Scripts looked up when the header has no <c>run</c>, in this order.</summary>
    private static readonly string[] DefaultScripts = ["run.ps1", "run.mjs", "run.js", "run.cs"];

    private static readonly FrozenDictionary<string, ToolScriptKind> Kinds = new Dictionary<string, ToolScriptKind>(StringComparer.OrdinalIgnoreCase)
    {
        [".ps1"] = ToolScriptKind.PowerShell,
        [".mjs"] = ToolScriptKind.Node,
        [".js"] = ToolScriptKind.Node,
        [".cs"] = ToolScriptKind.CSharp,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tool names are folder names: short, without spaces, safe in command ids.</summary>
    public static bool IsValidName(string name) => name is not null && NamePattern().IsMatch(name);

    /// <returns>The tool, or <c>null</c> when the folder is not a valid tool (the reason is in <paramref name="problems"/>).</returns>
    public static ToolDefinition? Read(IFileSystem fileSystem, string folder, bool personal, ICollection<string> problems)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(problems);
        var name = Path.GetFileName(folder);
        if (!IsValidName(name))
        {
            problems.Add(Format(Strings.ToolInvalidName, name));
            return null;
        }

        var header = FrontMatter.Parse(fileSystem.ReadAllText(Path.Combine(folder, ToolDefinition.DefinitionFile)));
        foreach (var problem in header.Errors.Concat(header.Keys.Where(key => !Keys.Contains(key)).Select(key => Format(Strings.ToolUnknownSetting, key))))
        {
            problems.Add($"{name}: {problem}");
        }

        if (Script(fileSystem, folder, header.Text(RunKey)) is not { } script || !Kinds.TryGetValue(Path.GetExtension(script), out var kind))
        {
            problems.Add(Format(Strings.ToolNoScript, name));
            return null;
        }

        var description = header.Text(DescriptionKey) ?? FirstLine(header.Body) ?? name;
        return new ToolDefinition(name, description, Parameters(header, name, problems), folder, script, kind, personal)
        {
            Timeout = Timeout(header.Text(TimeoutKey), name, problems),
        };
    }

    // The script must be a file inside the tool folder: "run" names a file, not a path.
    private static string? Script(IFileSystem fileSystem, string folder, string? run)
    {
        if (run is not null)
        {
            var file = Path.GetFileName(run);
            return file == run && fileSystem.FileExists(Path.Combine(folder, file)) ? Path.Combine(folder, file) : null;
        }

        return DefaultScripts.Select(file => Path.Combine(folder, file)).FirstOrDefault(fileSystem.FileExists);
    }

    private static List<ToolParameter> Parameters(FrontMatter header, string tool, ICollection<string> problems)
    {
        var map = header.Map(ParametersKey);
        var parameters = map.Count > 0
            ? map.Select(pair => new ToolParameter(pair.Key, pair.Value))
            : header.List(ParametersKey).Select(parameter => new ToolParameter(parameter, string.Empty));
        var valid = new List<ToolParameter>();
        foreach (var parameter in parameters)
        {
            if (ParameterPattern().IsMatch(parameter.Name) && valid.TrueForAll(existing => !existing.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase)))
            {
                valid.Add(parameter);
            }
            else
            {
                problems.Add($"{tool}: {Format(Strings.ToolInvalidParameter, parameter.Name)}");
            }
        }

        return valid;
    }

    private static TimeSpan Timeout(string? value, string tool, ICollection<string> problems)
    {
        if (value is null)
        {
            return ToolDefinition.DefaultTimeout;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds is > 0 and <= MaxTimeoutSeconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        problems.Add($"{tool}: {Format(Strings.ToolInvalidTimeout, value, MaxTimeoutSeconds)}");
        return ToolDefinition.DefaultTimeout;
    }

    private static string? FirstLine(string body) =>
        body.Split('\n').Select(line => line.Trim().TrimStart('#').Trim()).FirstOrDefault(line => line.Length > 0);

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}$", RegexOptions.IgnoreCase)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,31}$", RegexOptions.IgnoreCase)]
    private static partial Regex ParameterPattern();
}
