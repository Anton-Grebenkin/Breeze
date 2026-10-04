using System.Reflection;

namespace CodeEditor.Shell.Services;

/// <summary>
/// The product's version and origin: shown by "Help: About", used by updates and issue links. Read from the app
/// assembly: the version comes from the release tag (CI passes <c>-p:Version</c>), the SDK appends the commit after
/// "+", the repository comes from the <c>RepositoryUrl</c> assembly metadata (empty in local builds).
/// </summary>
public sealed record ProductInfo(string Name, string Version, string? Commit, Uri? Repository)
{
    public const string RepositoryUrlKey = "RepositoryUrl";

    private const char BuildMetadataSeparator = '+';
    private const int ShortCommitLength = 7;

    /// <summary>A SemVer pre-release such as <c>0.1.0-alpha.1</c>: updates include other pre-releases.</summary>
    public bool IsPrerelease => Version.Contains('-', StringComparison.Ordinal);

    /// <summary>The commit as git shows it in short form; <c>null</c> when the build didn't record it.</summary>
    public string? ShortCommit => Commit is { Length: > ShortCommitLength } commit ? commit[..ShortCommitLength] : Commit;

    public static ProductInfo FromAssembly(Assembly assembly, string name)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
        var separator = informational.IndexOf(BuildMetadataSeparator, StringComparison.Ordinal);
        var version = separator < 0 ? informational : informational[..separator];
        var commit = separator < 0 ? null : informational[(separator + 1)..];
        return new ProductInfo(name, version, string.IsNullOrEmpty(commit) ? null : commit, RepositoryOf(assembly));
    }

    private static Uri? RepositoryOf(Assembly assembly)
    {
        var url = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == RepositoryUrlKey)?.Value;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }
}
