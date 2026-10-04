using System.Globalization;
using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>
/// Enforces agent.md before a tool call: a change to a <c>protected</c> path or an <c>ask_before</c> action always
/// gets a card with the reason, even when the tool is allowed for the chat. Rules for some files reach the model with
/// the first read of a matching file; an edit of a file whose rule the model has not seen is returned with the rule.
/// </summary>
public sealed class ProjectRuleGuard(ProjectInstructions instructions, IWorkspace workspace)
{
    public const string ReadFileTool = "read_file";

    private const string CommandArgument = "command";

    private readonly HashSet<string> _delivered = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    /// <summary>Why the user must decide this call; <c>null</c> if agent.md does not require it.</summary>
    /// <param name="arguments">Call arguments: a command of a call without preview is taken from <c>command</c>.</param>
    /// <param name="changes">Preview of the call; empty for calls approved by a module policy.</param>
    public string? AskReason(IDictionary<string, object?> arguments, IReadOnlyList<FileChangePreview> changes)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(changes);
        var rules = instructions.Load();
        if (rules.Protected.IsEmpty && rules.AskBefore.Count == 0)
        {
            return null;
        }

        var reasons = new List<string>();
        if (Paths(changes).FirstOrDefault(path => rules.Protected.Matches(path)) is { } path)
        {
            reasons.Add(Format(Strings.RulesProtectedPath, path));
        }

        var commands = Commands(arguments, changes);
        reasons.AddRange(AskBeforeCategories.Detect(changes, commands)
            .Where(rules.AskBefore.Contains)
            .Select(category => Format(Strings.RulesAskBefore, AskBeforeCategories.Title(category))));
        return reasons.Count == 0 ? null : string.Join(' ', reasons);
    }

    /// <summary>Rules for the changed files that the model has not seen yet; they count as seen now.</summary>
    /// <returns>The answer to the model instead of the change; <c>null</c> if there are no new rules.</returns>
    public string? RulesBeforeEdit(IReadOnlyList<FileChangePreview> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return Deliver(Paths(changes)) is { } rules ? Strings.RulesBeforeEdit + "\n" + rules : null;
    }

    /// <summary>Rules for a file the model reads; they count as seen now.</summary>
    /// <returns>Text to append to the file; <c>null</c> if there are no new rules.</returns>
    public string? RulesForRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Relative(path) is { } relative && Deliver([relative]) is { } rules ? "\n\n" + rules : null;
    }

    /// <summary>New chat: the model has seen no rules.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _delivered.Clear();
        }
    }

    private string? Deliver(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var rule in instructions.Load().Scoped)
        {
            if (paths.Any(path => rule.Filter.Matches(path)) && MarkDelivered(rule.Source))
            {
                text.Append(CultureInfo.InvariantCulture, $"<project_rule source=\"{rule.Source}\" applies=\"{string.Join(", ", rule.Applies)}\">\n{rule.Text}\n</project_rule>\n");
            }
        }

        return text.Length == 0 ? null : text.ToString().TrimEnd();
    }

    private bool MarkDelivered(string source)
    {
        lock (_lock)
        {
            return _delivered.Add(source);
        }
    }

    private static List<string> Paths(IReadOnlyList<FileChangePreview> changes) =>
        [.. changes
            .Where(change => change.Kind != ProposedChangeKind.Command)
            .SelectMany(change => new[] { change.RelativePath, change.NewRelativePath })
            .OfType<string>()
            .Select(Normalize)
            .Where(path => path.Length > 0)];

    private static List<string> Commands(IDictionary<string, object?> arguments, IReadOnlyList<FileChangePreview> changes)
    {
        var commands = changes.Where(change => change.Kind == ProposedChangeKind.Command).Select(change => change.NewText).ToList();
        if (arguments.TryGetValue(CommandArgument, out var command) && command?.ToString() is { Length: > 0 } text)
        {
            commands.Add(text);
        }

        return commands;
    }

    // Read paths come from the model: relative, "./x", or full inside the folder.
    private string? Relative(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            return Normalize(path);
        }

        var relative = workspace.Root is null ? null : workspace.RelativePath(path);
        return relative is null || relative.StartsWith("..", StringComparison.Ordinal) ? null : Normalize(relative);
    }

    private static string Normalize(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }

    private static string Format(string format, string value) => string.Format(CultureInfo.CurrentCulture, format, value);
}
