using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Commands;

/// <summary>
/// Hints for non-obvious exit codes (ADR 0012): 1 from rg, findstr and grep means "no matches", from
/// <c>git diff --exit-code</c> "there are differences"; 1–7 from robocopy is success. Without a hint the model treats
/// them as errors and fixes what isn't broken.
/// </summary>
public static class CommandExitHints
{
    private static readonly string[] SearchTools = ["rg", "findstr", "grep"];

    /// <returns>A hint, or <c>null</c> if the exit code speaks for itself.</returns>
    public static string? For(string command, int exitCode)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (exitCode == 0)
        {
            return null;
        }

        var segments = CommandTokenizer.Parse(command).Segments;
        if (exitCode == 1 && segments.Any(words => SearchTools.Contains(ReadOnlyCommands.CommandName(words[0]))))
        {
            return Strings.ExitNoMatches;
        }

        if (exitCode == 1 && segments.Any(IsGitDiffExitCode))
        {
            return Strings.ExitDifferences;
        }

        return exitCode is > 0 and < 8 && segments.Any(words => ReadOnlyCommands.CommandName(words[0]) == "robocopy")
            ? Strings.ExitRobocopySuccess
            : null;
    }

    private static bool IsGitDiffExitCode(IReadOnlyList<string> words) =>
        ReadOnlyCommands.CommandName(words[0]) == "git" && words.Contains("diff") && words.Any(word => word is "--exit-code" or "--quiet");
}
