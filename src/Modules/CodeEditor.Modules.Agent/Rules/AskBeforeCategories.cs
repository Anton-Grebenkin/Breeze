using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>
/// Actions that <c>ask_before</c> in agent.md turns into a mandatory card. Recognized by the change itself, not by
/// the tool: a package can come from an edited project file or from a command.
/// </summary>
public static partial class AskBeforeCategories
{
    public const string NewPackage = "new-package";
    public const string PublicApi = "public-api";
    public const string DeleteTests = "delete-tests";
    public const string Migration = "migration";

    public static FrozenSet<string> All { get; } = FrozenSet.ToFrozenSet([NewPackage, PublicApi, DeleteTests, Migration], StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ProjectFileExtensions = FrozenSet.ToFrozenSet(
        [".csproj", ".fsproj", ".vbproj", ".props", ".targets"], StringComparer.OrdinalIgnoreCase);

    public static string Title(string category) => category switch
    {
        NewPackage => Strings.AskBeforeNewPackage,
        PublicApi => Strings.AskBeforePublicApi,
        DeleteTests => Strings.AskBeforeDeleteTests,
        Migration => Strings.AskBeforeMigration,
        _ => category,
    };

    /// <param name="changes">File changes of the call.</param>
    /// <param name="commands">Command lines the call runs.</param>
    public static IEnumerable<string> Detect(IReadOnlyList<FileChangePreview> changes, IReadOnlyList<string> commands)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(commands);
        var files = changes.Where(change => change.Kind != ProposedChangeKind.Command).ToList();
        if (files.Any(AddsPackage) || commands.Any(command => PackageCommand().IsMatch(command)))
        {
            yield return NewPackage;
        }

        if (files.Any(change => IsContracts(change.RelativePath) || IsContracts(change.NewRelativePath)))
        {
            yield return PublicApi;
        }

        if (files.Any(RemovesTests))
        {
            yield return DeleteTests;
        }

        if (files.Any(change => change.Kind == ProposedChangeKind.Create && HasSegment(change.RelativePath, "migrations"))
            || commands.Any(command => MigrationCommand().IsMatch(command)))
        {
            yield return Migration;
        }
    }

    private static bool AddsPackage(FileChangePreview change)
    {
        if (change.Kind is not (ProposedChangeKind.Edit or ProposedChangeKind.Create))
        {
            return false;
        }

        var name = Path.GetFileName(change.RelativePath);
        Func<string, bool>? isPackage =
            ProjectFileExtensions.Contains(Path.GetExtension(name)) || name.Equals("packages.config", StringComparison.OrdinalIgnoreCase)
                ? line => NuGetPackageLine().IsMatch(line)
                : name.Equals("package.json", StringComparison.OrdinalIgnoreCase) ? line => NpmDependencyLine().IsMatch(line)
                : name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    ? line => line.Trim() is { Length: > 0 } text && !text.StartsWith('#')
                : null;
        return isPackage is not null && AddedLines(change).Any(isPackage);
    }

    private static bool RemovesTests(FileChangePreview change) => change.Kind switch
    {
        ProposedChangeKind.Delete => IsTestPath(change.RelativePath),
        ProposedChangeKind.Edit => TestCase().Count(change.OldText) > TestCase().Count(change.NewText),
        _ => false,
    };

    // O(old + new) with a set of old lines.
    private static IEnumerable<string> AddedLines(FileChangePreview change)
    {
        var old = change.OldText.Split('\n').Select(line => line.Trim()).ToHashSet(StringComparer.Ordinal);
        return change.NewText.Split('\n').Where(line => !old.Contains(line.Trim()));
    }

    private static bool IsContracts(string? path) =>
        path is not null && Segments(path).Any(segment => segment.Equals("Contracts", StringComparison.OrdinalIgnoreCase)
            || segment.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase));

    private static bool IsTestPath(string path)
    {
        var segments = Segments(path);
        var name = segments.Length == 0 ? string.Empty : segments[^1];
        return segments.Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("tests", StringComparison.OrdinalIgnoreCase)
                || segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            || TestFileName().IsMatch(name);
    }

    private static bool HasSegment(string path, string folder) => Segments(path).Any(segment => segment.Equals(folder, StringComparison.OrdinalIgnoreCase));

    private static string[] Segments(string path) => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"<(PackageReference|PackageVersion|package)\s", RegexOptions.IgnoreCase)]
    private static partial Regex NuGetPackageLine();

    // "name": "^1.2.3" — a dependency, not the package's own "version".
    [GeneratedRegex(@"^\s*""(?!version"")[^""]+""\s*:\s*""[\^~>=<]*\d")]
    private static partial Regex NpmDependencyLine();

    // "npm install" alone restores packages; with a name it adds one.
    [GeneratedRegex(@"\b(dotnet\s+add\s+(\S+\s+)?package\b|nuget\s+install\b|Install-Package\b|(npm|pnpm)\s+(install|i|add)\s+[^-\s]|yarn\s+add\b|pip3?\s+install\b|poetry\s+add\b|cargo\s+add\b|go\s+get\b)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageCommand();

    [GeneratedRegex(@"\b(dotnet\s+ef\s+migrations\s+add|prisma\s+migrate|alembic\s+revision|rails\s+generate\s+migration)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MigrationCommand();

    [GeneratedRegex(@"\[(Fact|Theory|Test|TestMethod|TestCase)\b|^\s*(it|test)\s*\(", RegexOptions.Multiline)]
    private static partial Regex TestCase();

    [GeneratedRegex(@"(Tests?\.(cs|fs|vb)|\.(test|spec)\.[a-z]+|^test_.+\.py|_test\.(py|go))$", RegexOptions.IgnoreCase)]
    private static partial Regex TestFileName();
}
