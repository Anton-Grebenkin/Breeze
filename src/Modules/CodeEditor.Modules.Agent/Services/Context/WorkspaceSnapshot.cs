using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts.Files;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// Workspace snapshot for the first chat message (ADR 0012): solutions, projects with target frameworks, manifests of
/// other ecosystems, top folders with file counts, docs, the git branch and command-line tools on the machine
/// (<see cref="EnvironmentTools"/>). The model does not have to map the folder again. The text is model data, in
/// English like the prompt, at most <see cref="MaxCharacters"/> long.
/// </summary>
public sealed partial class WorkspaceSnapshot(IWorkspace workspace, IFileIndex index, IFileSystem fileSystem, EnvironmentTools tools)
{
    public const int MaxCharacters = 3_000;
    public const int MaxProjects = 40;
    public const int MaxFolders = 15;
    public const int MaxDocs = 10;

    /// <summary>How long to wait for the initial index build, so a large folder does not delay the first message.</summary>
    private static readonly TimeSpan IndexWait = TimeSpan.FromSeconds(2);

    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];
    private static readonly string[] SolutionExtensions = [".sln", ".slnx", ".slnf"];
    private static readonly FrozenSet<string> Manifests = FrozenSet.ToFrozenSet(
        ["package.json", "pyproject.toml", "requirements.txt", "go.mod", "Cargo.toml", "pom.xml", "build.gradle", "build.gradle.kts", "Dockerfile", "docker-compose.yml"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly string[] RootDocs = ["README", "CONTRIBUTING", "CHANGELOG", "ARCHITECTURE", "AGENTS", "CLAUDE"];
    private static readonly string[] TestMarkers = ["Microsoft.NET.Test.Sdk", "xunit", "MSTest", "NUnit", "TUnit"];

    /// <returns>The snapshot, or <c>null</c> if no folder is open.</returns>
    public async Task<string?> BuildAsync(CancellationToken cancellationToken)
    {
        if (workspace.Root is not { } root)
        {
            return null;
        }

        await Task.WhenAny(index.WhenReady, Task.Delay(IndexWait, cancellationToken));
        var files = index.Files.Where(file => !SensitivePaths.IsAgentData(file.RelativePath)).ToList();
        var lines = new List<string> { string.Create(CultureInfo.InvariantCulture, $"Folder: {workspace.Name}, {files.Count} files.") };
        AddList(lines, "Solutions", files.Where(file => HasExtension(file, SolutionExtensions)).Select(file => file.RelativePath), MaxDocs);
        AddList(lines, "Projects", Projects(files), MaxProjects);
        AddList(lines, "Manifests", files.Where(static file => Manifests.Contains(file.Name)).Select(static file => file.RelativePath), MaxDocs);
        AddList(lines, "Top folders", TopFolders(files), MaxFolders);
        AddList(lines, "Docs", Docs(files), MaxDocs);
        if (GitBranch(root) is { } branch)
        {
            lines.Add("Git branch: " + branch);
        }

        if (await tools.DescribeAsync() is { } environment)
        {
            lines.Add(environment);
        }

        var text = string.Join('\n', lines);
        return text.Length <= MaxCharacters ? text : string.Concat(text.AsSpan(0, MaxCharacters), "…");
    }

    private static void AddList(List<string> lines, string title, IEnumerable<string> items, int limit)
    {
        var all = items.ToList();
        if (all.Count == 0)
        {
            return;
        }

        var shown = string.Join(", ", all.Take(limit));
        lines.Add(all.Count > limit
            ? string.Create(CultureInfo.InvariantCulture, $"{title} ({all.Count}): {shown}, … {all.Count - limit} more")
            : string.Create(CultureInfo.InvariantCulture, $"{title} ({all.Count}): {shown}"));
    }

    // Project folder and framework: "src/App (net10.0)", test projects marked "tests". Only shown projects are read;
    // the rest are just counted.
    private IEnumerable<string> Projects(List<IndexedFile> files) =>
        files.Where(file => HasExtension(file, ProjectExtensions))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select((project, position) => position < MaxProjects ? Describe(project) : project.RelativePath);

    private string Describe(IndexedFile project)
    {
        var folder = Path.GetDirectoryName(project.RelativePath)?.Replace('\\', '/') is { Length: > 0 } directory ? directory : project.Name;
        var text = ReadSmall(project.FullPath);
        var framework = TargetFramework().Match(text) is { Success: true } match ? match.Groups["value"].Value.Trim() : null;
        var isTest = TestMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)) || project.Name.Contains("Tests", StringComparison.Ordinal);
        var notes = new[] { framework, isTest ? "tests" : null }.OfType<string>().ToList();
        return notes.Count == 0 ? folder : $"{folder} ({string.Join(", ", notes)})";
    }

    private static IEnumerable<string> TopFolders(List<IndexedFile> files) =>
        files.Select(file => file.RelativePath.IndexOf('/') is var slash and > 0 ? file.RelativePath[..slash] + "/" : null)
            .GroupBy(folder => folder ?? string.Empty)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key.Length == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{group.Count()} files in the root")
                : string.Create(CultureInfo.InvariantCulture, $"{group.Key} ({group.Count()})"));

    private static IEnumerable<string> Docs(List<IndexedFile> files) =>
        files.Where(file => (!file.RelativePath.Contains('/') && RootDocs.Any(doc => file.Name.StartsWith(doc, StringComparison.OrdinalIgnoreCase)))
                || (file.RelativePath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.AsSpan().Count('/') == 1 && file.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(file => file.RelativePath.Contains('/') ? 1 : 0)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(file => file.RelativePath);

    // "ref: refs/heads/main" is a branch; otherwise a detached HEAD shown by its hash prefix.
    private string? GitBranch(string root)
    {
        var head = ReadSmall(Path.Combine(root, ".git", "HEAD")).Trim();
        const string reference = "ref: refs/heads/";
        return head.StartsWith(reference, StringComparison.Ordinal) ? head[reference.Length..]
            : head.Length >= 8 ? "detached at " + head[..8]
            : null;
    }

    private string ReadSmall(string path)
    {
        try
        {
            return fileSystem.FileExists(path) ? fileSystem.ReadAllText(path) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool HasExtension(IndexedFile file, string[] extensions) =>
        extensions.Any(extension => file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"<TargetFrameworks?>(?<value>[^<]+)</TargetFrameworks?>", RegexOptions.CultureInvariant)]
    private static partial Regex TargetFramework();
}
