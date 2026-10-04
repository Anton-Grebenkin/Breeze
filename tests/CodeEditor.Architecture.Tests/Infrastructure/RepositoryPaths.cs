namespace CodeEditor.Architecture.Tests.Infrastructure;

/// <summary>
/// Repository paths, located from the test build folder.
/// </summary>
internal static class RepositoryPaths
{
    private const string SolutionFileName = "CodeEditor.slnx";

    public static string Root { get; } = FindRoot();

    public static string Source => Path.Combine(Root, "src");

    /// <summary>All .cs files in src except generated ones in obj and bin.</summary>
    public static IEnumerable<string> SourceFiles() => SourceFiles("*.cs");

    /// <summary>Files matching the pattern in src except generated ones in obj and bin.</summary>
    public static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Source, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    public static string Relative(string path) => Path.GetRelativePath(Root, path);

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Не найден {SolutionFileName} выше {AppContext.BaseDirectory}.");
    }
}
