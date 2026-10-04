namespace CodeEditor.Modules.Git.Services.Cli;

/// <summary>git output lines, stdout and stderr in arrival order.</summary>
/// <param name="Truncated">The line limit was hit.</param>
public sealed record GitOutput(IReadOnlyList<string> Lines, bool Truncated)
{
    /// <summary>The output as one string: <c>-z</c> records stay NUL-separated, newlines inside are restored.</summary>
    public string Text => string.Join('\n', Lines);
}
