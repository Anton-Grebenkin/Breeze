namespace CodeEditor.Core.Processes;

/// <summary>
/// What to run: program, arguments (kept as a list, so no quoting issues), working folder and timeout.
/// </summary>
public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory)
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Extra environment variables on top of the current ones.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Removes secret-looking variables (<see cref="SecretVariables"/>) so an agent command cannot see keys that
    /// would then reach the model in its output.
    /// </summary>
    public bool HideSecretVariables { get; init; }
}
