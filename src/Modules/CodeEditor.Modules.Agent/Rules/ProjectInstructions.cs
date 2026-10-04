using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Core.Settings;
using CodeEditor.Core.Storage;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>
/// Reads the rules the agent follows: <c>.breeze/agent.md</c> (header with settings plus text), <c>AGENTS.md</c> and
/// <c>CLAUDE.md</c> together, rules for some files in <c>.breeze/rules/*.md</c> and personal <c>agent.md</c> in the
/// user data folder. Every call checks file sizes and write times, so edits apply without a restart, while unchanged
/// files are not read again: the guard asks for the rules on every read and change the agent makes.
/// </summary>
public sealed class ProjectInstructions(IWorkspace workspace, IFileSystem fileSystem, UserDataPaths paths, RulesProblemReporter? reporter = null)
{
    public const int MaxLines = 200;
    public const int MaxCharacters = 16_000;
    public const string FileName = "agent.md";

    /// <summary>Main rules file of the folder.</summary>
    public const string AgentFile = SettingsService.WorkspaceFolder + "/" + FileName;

    public const string RulesFolder = SettingsService.WorkspaceFolder + "/rules";

    // Rules file before agent.md: read when there is no agent.md.
    private const string LegacyFile = SettingsService.WorkspaceFolder + "/instructions.md";

    private const int MaxScopedRules = 50;

    private static readonly string[] SharedFiles = ["AGENTS.md", "CLAUDE.md"];

    private readonly Lock _lock = new();
    private string? _stamp;
    private ProjectRuleSet? _cached;

    private enum RuleFileKind
    {
        Agent,
        Text,
        Scoped,
        Personal,
    }

    /// <summary>Personal rules file: applies in every folder.</summary>
    public string PersonalFile => paths.File(FileName);

    public ProjectRuleSet Load()
    {
        var files = RuleFiles();
        var stamp = Stamp(files);
        lock (_lock)
        {
            if (_cached is not null && stamp == _stamp)
            {
                return _cached;
            }
        }

        var rules = Collect(files);
        lock (_lock)
        {
            _cached = rules;
            _stamp = stamp;
        }

        reporter?.Report(rules.Problems);
        return rules;
    }

    /// <summary>Creates <c>.breeze/agent.md</c> from the template when it is missing.</summary>
    /// <returns>The full path; <c>null</c> if no folder is open.</returns>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder is read-only.</exception>
    public string? EnsureAgentFile()
    {
        if (workspace.Root is not { } root)
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(root, AgentFile));
        var folder = Path.Combine(root, SettingsService.WorkspaceFolder);
        if (!fileSystem.FileExists(path))
        {
            if (!fileSystem.DirectoryExists(folder))
            {
                fileSystem.CreateDirectory(folder);
            }

            fileSystem.WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(Strings.AgentRulesTemplate.ReplaceLineEndings("\n")));
        }

        return path;
    }

    /// <summary>The file changes the rules: the agent is rebuilt with the new prompt.</summary>
    public static bool IsRulesFile(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var path = relativePath.Replace('\\', '/');
        return path.Equals(AgentFile, StringComparison.OrdinalIgnoreCase)
            || path.Equals(LegacyFile, StringComparison.OrdinalIgnoreCase)
            || SharedFiles.Contains(path, StringComparer.OrdinalIgnoreCase)
            || path.StartsWith(RulesFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    // Existing rule files in reading order: folder rules first, personal last.
    private List<RuleFile> RuleFiles()
    {
        var files = new List<RuleFile>();
        if (workspace.Root is { } root)
        {
            var agent = Path.Combine(root, AgentFile);
            files.Add(fileSystem.FileExists(agent)
                ? new RuleFile(AgentFile, agent, RuleFileKind.Agent)
                : new RuleFile(LegacyFile, Path.Combine(root, LegacyFile), RuleFileKind.Text));
            files.AddRange(SharedFiles.Select(name => new RuleFile(name, Path.Combine(root, name), RuleFileKind.Text)));
            files.AddRange(ScopedRuleFiles(root));
        }

        files.Add(new RuleFile(FileName, PersonalFile, RuleFileKind.Personal));
        return files.FindAll(file => fileSystem.FileExists(file.Path));
    }

    private IEnumerable<RuleFile> ScopedRuleFiles(string root)
    {
        var folder = Path.Combine(root, RulesFolder);
        if (!fileSystem.DirectoryExists(folder))
        {
            return [];
        }

        return fileSystem.EnumerateEntries(folder)
            .Where(static entry => !entry.IsDirectory && entry.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxScopedRules)
            .Select(static entry => new RuleFile(RulesFolder + "/" + entry.Name, entry.FullPath, RuleFileKind.Scoped));
    }

    private ProjectRuleSet Collect(List<RuleFile> files)
    {
        var collector = new RuleCollector();
        foreach (var file in files)
        {
            if (Read(file.Path) is not { } text)
            {
                continue;
            }

            switch (file.Kind)
            {
                case RuleFileKind.Agent or RuleFileKind.Personal:
                    collector.AddAgentFile(file.Source, text, personal: file.Kind == RuleFileKind.Personal);
                    break;
                case RuleFileKind.Scoped:
                    collector.AddScopedRule(file.Source, text);
                    break;
                default:
                    collector.AddText(file.Source, text);
                    break;
            }
        }

        return collector.Build(MaxLines, MaxCharacters);
    }

    // Folder, file list, sizes and write times: a change in any of them means the rules are read again.
    private string Stamp(List<RuleFile> files)
    {
        var stamp = new StringBuilder(workspace.Root);
        foreach (var file in files)
        {
            stamp.Append('|').Append(file.Path).Append(':');
            try
            {
                stamp.Append(fileSystem.GetFileLength(file.Path)).Append(':').Append(fileSystem.GetLastWriteTimeUtc(file.Path).Ticks);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                stamp.Append('?');
            }
        }

        return stamp.ToString();
    }

    private string? Read(string path)
    {
        try
        {
            return fileSystem.FileExists(path) && fileSystem.ReadAllText(path).Trim() is { Length: > 0 } text ? text : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private readonly record struct RuleFile(string Source, string Path, RuleFileKind Kind);
}
