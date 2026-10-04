using System.Globalization;
using CodeEditor.Core.Processes;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Git.Resources;

namespace CodeEditor.Modules.Git.Services.Cli;

/// <summary>
/// git for the panel (via <see cref="GitRunner"/>): full output as lines (the middle is not dropped), failures as
/// <see cref="GitException"/> with git's text. Repository commands run in its root (<c>-C</c>) with literal pathspecs,
/// so "a[1].txt" is a file name, not a pattern. git starts and its output is handled on a background thread, keeping
/// process start and large-output parsing off the UI thread.
/// </summary>
public sealed class GitCli(GitRunner runner)
{
    /// <summary>Output line limit: a huge diff is truncated with a notice.</summary>
    public const int MaxLines = 200_000;

    // "git diff --no-index" exits with 1 when the files differ.
    private const int DifferencesExitCode = 1;

    /// <summary>
    /// Reads without writing to .git (<c>--no-optional-locks</c>): otherwise <c>git status</c> and <c>git diff</c>
    /// rewrite the index, and the .git watcher would trigger another refresh.
    /// </summary>
    /// <param name="root">Repository root; <c>null</c> means the workspace (repository lookup, <c>git init</c>).</param>
    /// <param name="differencesAreSuccess">Treat exit code 1 as success (<c>git diff --no-index</c>).</param>
    /// <exception cref="GitException">git failed, timed out or is not installed.</exception>
    public async Task<GitOutput> ReadAsync(string? root, IReadOnlyList<string> arguments, CancellationToken cancellationToken, bool differencesAreSuccess = false)
    {
        var lines = new LineCollector(MaxLines);
        var result = await StartAsync(root, ["--no-optional-locks", .. arguments], lines.Add, GitRunner.Timeout, cancellationToken).ConfigureAwait(false);
        var success = result.ExitCode == 0 || (differencesAreSuccess && result.ExitCode == DifferencesExitCode);
        return success ? lines.ToOutput() : throw Failure(result);
    }

    /// <exception cref="GitException">git failed, timed out or is not installed.</exception>
    public async Task RunAsync(string? root, IReadOnlyList<string> arguments, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var result = await StartAsync(root, arguments, onLine: null, timeout ?? GitRunner.Timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw Failure(result);
        }
    }

    private async Task<ProcessResult> StartAsync(
        string? root, IReadOnlyList<string> arguments, Action<string>? onLine, TimeSpan timeout, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> scoped = root is null ? arguments : ["--literal-pathspecs", "-C", root, .. arguments];
        ProcessResult result;
        try
        {
            // Process start is synchronous and can take tens of ms (antivirus scan): keep it off the UI thread.
            result = await Task.Run(() => runner.RunAsync(scoped, onLine, cancellationToken, timeout), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            throw new GitException(exception.Message, exception) { IsNotInstalled = true };
        }
        catch (AgentToolException exception)
        {
            throw new GitException(exception.Message, exception);
        }

        return result.TimedOut ? throw new GitException(Format(Strings.GitTimeout, (int)timeout.TotalSeconds)) : result;
    }

    private static GitException Failure(ProcessResult result)
    {
        var output = result.Output.Trim();
        return new GitException(output.Length > 0 ? output : Format(Strings.GitExitCode, result.ExitCode)) { ExitCode = result.ExitCode };
    }

    private static string Format(string format, int argument) => string.Format(CultureInfo.CurrentCulture, format, argument);

    // Lines arrive from two background threads (stdout and stderr), hence the lock.
    private sealed class LineCollector(int limit)
    {
        private readonly Lock _gate = new();
        private readonly List<string> _lines = [];
        private bool _truncated;

        public void Add(string line)
        {
            lock (_gate)
            {
                if (_lines.Count < limit)
                {
                    _lines.Add(line);
                }
                else
                {
                    _truncated = true;
                }
            }
        }

        public GitOutput ToOutput()
        {
            lock (_gate)
            {
                return new GitOutput([.. _lines], _truncated);
            }
        }
    }
}
