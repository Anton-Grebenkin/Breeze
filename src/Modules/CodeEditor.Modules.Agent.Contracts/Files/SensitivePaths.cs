using System.Collections.Frozen;
using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Resources;

namespace CodeEditor.Modules.Agent.Contracts.Files;

/// <summary>
/// Deterministic agent tool guards (a "Swiss cheese" layer before user approval): the agent does not read secret
/// files, whose contents would go to an external model, and does not edit service folders (git history, build output,
/// chat history).
/// </summary>
public static class SensitivePaths
{
    private static readonly FrozenSet<string> SecretExtensions = FrozenSet.ToFrozenSet(
        [".pfx", ".p12", ".pem", ".key", ".snk", ".jks", ".keystore", ".publishsettings"], StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> SecretNames = FrozenSet.ToFrozenSet(
        ["id_rsa", "id_ed25519", "id_ecdsa", "secrets.json", ".netrc", ".npmrc", ".pypirc"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Environment samples are not secrets: they are read to understand the settings.</summary>
    private static readonly FrozenSet<string> EnvSamples = FrozenSet.ToFrozenSet(
        [".env.example", ".env.sample", ".env.template"], StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ProtectedFolders = FrozenSet.ToFrozenSet(
        [".git", "bin", "obj", ".vs", "node_modules"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Agent data (chat history, tool outputs, memory) inside <c>.breeze</c>; everything else there (rules, folder
    /// settings) may be edited.
    /// </summary>
    public const string AgentDataFolder = ".breeze/agent";

    /// <summary>Tool outputs saved to files (<see cref="IAgentOutputStore"/>).</summary>
    public const string AgentOutputsFolder = AgentDataFolder + "/outputs";

    /// <summary>
    /// Agent data: agent search and file lists skip it, otherwise other conversations and old logs leak into the context.
    /// </summary>
    public static bool IsAgentData(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return IsUnder(relativePath, AgentDataFolder);
    }

    /// <summary>A saved tool output: the model reads and searches it via the link in the preview.</summary>
    public static bool IsAgentOutput(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return IsUnder(relativePath, AgentOutputsFolder);
    }

    // Called per file when listing the workspace: compares spans instead of building substrings.
    private static bool IsUnder(string relativePath, string folder)
    {
        var normalized = relativePath.Replace('\\', '/').AsSpan();
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
            && (normalized.Length == folder.Length || normalized[folder.Length] == '/');
    }

    public static bool IsSecret(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var name = Path.GetFileName(relativePath);
        if (EnvSamples.Contains(name))
        {
            return false;
        }

        return name.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
            || SecretNames.Contains(name)
            || SecretExtensions.Contains(Path.GetExtension(name));
    }

    public static bool IsProtected(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var normalized = relativePath.Replace('\\', '/');
        return IsAgentData(normalized) || normalized.Split('/').Any(ProtectedFolders.Contains);
    }

    /// <exception cref="AgentToolException">The file looks like a secrets file.</exception>
    public static void EnsureReadable(string relativePath)
    {
        if (IsSecret(relativePath))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.SecretFile, relativePath));
        }
    }

    /// <exception cref="AgentToolException">A secrets file or a service folder.</exception>
    public static void EnsureWritable(string relativePath)
    {
        EnsureReadable(relativePath);
        if (IsProtected(relativePath))
        {
            throw new AgentToolException(string.Format(CultureInfo.CurrentCulture, Strings.ProtectedFolder, relativePath));
        }
    }
}
