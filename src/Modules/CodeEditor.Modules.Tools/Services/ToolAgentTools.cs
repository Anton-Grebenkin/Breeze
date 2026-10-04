using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Tools.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Tools.Services;

/// <summary>
/// <c>run_tool</c> for the agent: runs a tool from the shelf by name. A tool whose code the user trusts runs without
/// a card; new or changed code always gets a card with the code, also after "Allow for this chat". "Always allow"
/// trusts this version of the tool.
/// </summary>
public sealed class ToolAgentTools(ToolShelf shelf, ToolRunner runner, ToolTrust trust, ToolActivity activity, IWorkspace workspace, IFileSystem fileSystem, IAgentOutputStore outputs)
    : IAgentToolProvider, IAgentChangePreviewer, IAgentApprovalPolicy
{
    public const string RunToolName = "run_tool";

    private const int PreviewLines = 60;

    // Hash shown on the card: "Always allow" trusts exactly the code the user saw.
    private readonly ConcurrentDictionary<string, string> _shown = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<AITool> CreateTools() => workspace.Root is null && shelf.Tools.IsEmpty
        ? []
        : [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RunAsync, RunToolName,
            "Runs a tool from the project's tool shelf; the shelf is listed in <context>. Pass the tool name and its parameters by name. " +
            "Tools are scripts in .breeze/tools/<name>/: tool.md with a header (description; parameters: name: what it is) and " +
            "run.ps1, run.mjs or run.cs that gets the parameters as -name value (PowerShell) or --name value. " +
            "Create one when the user asks for a reusable tool. The output is returned; long output is saved to a file."))];

    public bool CanDecide(string toolName) => toolName == RunToolName;

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) => Find(arguments) is { } tool && trust.IsTrusted(tool);

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) =>
        Find(arguments) is { IsPersonal: false } tool && _shown.TryGetValue(tool.Name, out _) ? tool.Name : null;

    public void AllowAlways(string toolName, string rule)
    {
        if (_shown.TryRemove(rule, out var hash))
        {
            trust.Trust(hash);
        }
    }

    public bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) => Find(arguments) is not { } tool || !trust.IsTrusted(tool);

    public bool CanPreview(string toolName) => toolName == RunToolName;

    public Task<IReadOnlyList<FileChangePreview>> PreviewAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        var tool = Find(arguments) ?? throw new AgentToolException(NotFound(Name(arguments)));
        var request = runner.Request(tool, Parameters(arguments), forAgent: true);
        _shown[tool.Name] = trust.Hash(tool);
        var code = fileSystem.FileExists(tool.Script) ? Excerpt(fileSystem.ReadAllText(tool.Script)) : string.Empty;
        IReadOnlyList<FileChangePreview> preview =
        [
            new FileChangePreview(ProposedChangeKind.Command, workspace.RelativePath(tool.Folder), string.Empty, runner.Display(request) + "\n\n" + code)
            {
                Title = Format(Strings.ApprovalTitle, tool.Name),
                Header = Format(Strings.ApprovalHeader, workspace.RelativePath(tool.Script)),
            },
        ];
        return Task.FromResult(preview);
    }

    private async Task<string> RunAsync(
        [Description("Tool name from the shelf.")] string name,
        [Description("Parameters by name, for example {\"path\": \"src\"}; values are strings.")] Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var tool = shelf.Find(name) ?? throw new AgentToolException(NotFound(name));
        var request = runner.Request(tool, parameters ?? [], forAgent: true);
        activity.Started(tool.Name, runner.Display(request));
        var result = await runner.RunAsync(request, activity.Line, cancellationToken);
        activity.Finished(tool.Name, result);
        var output = result.Output.TrimEnd();
        if (result.TimedOut)
        {
            throw new AgentToolException(Format(Strings.ToolTimedOut, tool.Name, (int)tool.Timeout.TotalSeconds, outputs.Fit(output, RunToolName)));
        }

        return result.ExitCode != 0 ? throw new AgentToolException(Format(Strings.ToolFailed, tool.Name, result.ExitCode, outputs.Fit(output, RunToolName)))
            : output.Length == 0 ? Strings.ToolDoneEmpty
            : outputs.Fit(output, RunToolName);
    }

    private ToolDefinition? Find(IDictionary<string, object?> arguments) => Name(arguments) is { Length: > 0 } name ? shelf.Find(name) : null;

    private static string? Name(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("name", out var value) ? value?.ToString() : null;

    // Arguments come as a JSON object from the model or as a dictionary from tests.
    private static Dictionary<string, string> Parameters(IDictionary<string, object?> arguments) =>
        !arguments.TryGetValue("parameters", out var value) || value is null ? []
            : value is JsonElement { ValueKind: JsonValueKind.Object } json ? json.EnumerateObject().ToDictionary(property => property.Name, property => Text(property.Value))
            : value is IEnumerable<KeyValuePair<string, string>> pairs ? pairs.ToDictionary()
            : [];

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private string NotFound(string? name)
    {
        var names = string.Join(", ", shelf.Tools.Select(tool => tool.Name));
        return Format(Strings.ToolNotFound, name ?? string.Empty, names.Length > 0 ? names : "-");
    }

    private static string Excerpt(string code)
    {
        var lines = code.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        return lines.Length <= PreviewLines ? string.Join('\n', lines) : string.Join('\n', lines.Take(PreviewLines)) + "\n" + Format(Strings.MoreLines, lines.Length - PreviewLines);
    }

    private static string Format(string format, params object[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
