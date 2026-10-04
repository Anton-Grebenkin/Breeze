namespace CodeEditor.Core.Processes;

/// <summary>
/// Secret-looking environment variables, like Codex's environment filter: the name mentions a key, token, password
/// or credential. Agent commands run without them because their output goes to an external model.
/// </summary>
public static class SecretVariables
{
    private static readonly string[] Markers = ["KEY", "TOKEN", "SECRET", "PASSWORD", "PASSWD", "CREDENTIAL"];

    public static bool IsSecret(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Markers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
