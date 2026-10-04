using System.Diagnostics;
using System.Text;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Runs Cursor CLI on the same task, repository copy, user request and checks as our agent. The Cursor model is set
/// separately (<c>--cursor-model</c>) and cost uses the ProxyAPI price of the run's model, so tokens are compared, not
/// tariffs. Cursor runs commands without asking (<c>--force</c>), like our agent in runs. The event stream is kept in
/// <c>cursor.jsonl</c> in the run folder.
/// </summary>
internal sealed class CursorPlayer(EvalOptions options)
{
    public const string Mode = "cursor";

    public const string TimeoutError = "тайм-аут";

    public async Task<(EvalResult Result, string Answer)> PlayAsync(string repo, string folder, EvalTask task, CancellationToken cancellationToken)
    {
        using var process = Process.Start(StartInfo(repo, task.Prompt)) ?? throw new InvalidOperationException("Cursor CLI не запустился.");
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        await using var kill = timeout.Token.Register(() => Kill(process));

        var stream = new CursorStream();
        var clock = Stopwatch.StartNew();
        await using (var log = new StreamWriter(Path.Combine(folder, "cursor.jsonl"), append: false, Encoding.UTF8))
        {
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None) is { } line)
            {
                await log.WriteLineAsync(line);
                stream.Add(line);
            }
        }

        await process.WaitForExitAsync(CancellationToken.None);
        var result = new EvalResult(string.Empty, string.Empty, task.Id, Solved: false, string.Empty)
        {
            TurnError = timeout.IsCancellationRequested ? TimeoutError : stream.Error ?? ExitError(process.ExitCode, await errors),
            Requests = stream.Requests,
            ToolCalls = stream.ToolCalls,
            InputTokens = stream.InputTokens,
            CachedInputTokens = stream.CachedInputTokens,
            OutputTokens = stream.OutputTokens,
            Elapsed = clock.Elapsed,
        };
        return (result, stream.Answer);
    }

    private ProcessStartInfo StartInfo(string repo, string prompt)
    {
        var version = LatestVersion();
        var start = new ProcessStartInfo(Path.Combine(version, "node.exe"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = repo,
        };
        start.Environment["CURSOR_INVOKED_AS"] = "cursor-agent";
        string[] arguments =
        [
            Path.Combine(version, "index.js"), "--print", "--output-format", "stream-json", "--model", options.CursorModel,
            "--force", "--trust", "--sandbox", "disabled", "--workspace", repo, prompt,
        ];
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    // Like cursor-agent.ps1: the latest version in %LOCALAPPDATA%\cursor-agent\versions. Node is started directly rather
    // than via .cmd and PowerShell so a multiline prompt with quotes arrives intact.
    private static string LatestVersion()
    {
        var versions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cursor-agent", "versions");
        return Directory.Exists(versions) && Directory.GetDirectories(versions).Order(StringComparer.Ordinal).LastOrDefault() is { } latest
            ? latest
            : throw new InvalidOperationException($"Cursor CLI не установлен: нет {versions}.");
    }

    private static string? ExitError(int exitCode, string errors) =>
        exitCode == 0 ? null : $"Cursor завершился с кодом {exitCode}: {errors.Trim().ReplaceLineEndings(" ")}";

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
