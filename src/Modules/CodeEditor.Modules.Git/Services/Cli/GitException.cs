namespace CodeEditor.Modules.Git.Services.Cli;

/// <summary>git failed, timed out or is not installed. The message is git's output, shown in the panel.</summary>
public sealed class GitException : Exception
{
    public GitException()
    {
    }

    public GitException(string message)
        : base(message)
    {
    }

    public GitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>git is not found; the panel offers to install it.</summary>
    public bool IsNotInstalled { get; init; }

    /// <summary>git exit code; <c>0</c> when the process never produced one (did not start or timed out).</summary>
    public int ExitCode { get; init; }
}
