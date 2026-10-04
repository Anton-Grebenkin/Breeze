using System.Text.RegularExpressions;
using CodeEditor.Modules.Terminal.Services.Build;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Checks a task after the turn: the answer contains the expected text; for edits, hidden tests are in place, the build
/// is clean, all tests pass, protected files are unchanged and no forbidden text remains (comments are allowed). The
/// score is the share of hidden tests passed, showing how close the agent got. Builds and tests use the agent's own
/// services (unsaved tabs are saved first).
/// </summary>
internal sealed partial class EvalVerifier(DotNetBuild build, DotNetTests tests, string folder)
{
    /// <param name="originals">Text of the protected files before the turn.</param>
    public async Task<EvalVerdict> VerifyAsync(EvalTask task, string answer, IReadOnlyDictionary<string, string> originals, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        if (task.AnswerMustContain is { } expected && !answer.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"в ответе нет «{expected}»");
        }

        if (!task.ChangesCode)
        {
            return new EvalVerdict(problems, Score: null);
        }

        problems.AddRange(originals.Where(file => File.ReadAllText(Path.Combine(folder, file.Key)) != file.Value).Select(file => $"изменён {file.Key}"));
        if (task.Forbidden is { } pattern && FindForbidden(pattern) is { } offender)
        {
            problems.Add($"осталось «{pattern}» в {offender}");
        }

        if (task.HiddenTests is { } hidden)
        {
            // A copy keeps the old timestamp, and a newer build output would skip recompiling the tests.
            var target = Path.Combine(folder, task.HiddenTarget ?? task.TestsProject, Path.GetFileName(hidden));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(EvalRepository.HiddenFolder, hidden), target, overwrite: true);
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow);
        }

        var score = task.HiddenOnly
            ? await CheckHiddenOnlyAsync(task, problems, cancellationToken)
            : await CheckBuildAndTestsAsync(task, problems, cancellationToken);
        return new EvalVerdict(problems, task.HiddenTests is null ? null : score);
    }

    /// <returns>Share of hidden tests passed.</returns>
    private async Task<double> CheckBuildAndTestsAsync(EvalTask task, List<string> problems, CancellationToken cancellationToken)
    {
        var built = await build.BuildAsync(null, cancellationToken);
        if (!built.Succeeded)
        {
            problems.Add($"сборка: ошибок {built.Errors}");
            return 0;
        }

        var tested = await tests.RunAsync(null, null, cancellationToken);
        if (tested.Succeeded)
        {
            return 1;
        }

        problems.Add($"тесты: не прошло {tested.Failed} из {tested.Total}");
        return task.HiddenTests is { } hidden ? await HiddenScoreAsync(task, hidden, cancellationToken) : 0;
    }

    // External project: builds the test project (which pulls in the code) and runs only the hidden tests.
    private async Task<double> CheckHiddenOnlyAsync(EvalTask task, List<string> problems, CancellationToken cancellationToken)
    {
        var built = await build.BuildAsync(task.TestsProjectFile, cancellationToken);
        if (!built.Succeeded)
        {
            problems.Add($"сборка: ошибок {built.Errors}");
            return 0;
        }

        var report = await tests.RunAsync(task.TestsProjectFile, Path.GetFileNameWithoutExtension(task.HiddenTests!), cancellationToken);
        if (report.Total == 0 || report.Failed > 0)
        {
            problems.Add($"скрытые тесты: не прошло {report.Total - report.Passed} из {report.Total}");
        }

        return PassedShare(report);
    }

    // A separate run of the hidden tests only: the full run mixes visible and hidden failures.
    private async Task<double> HiddenScoreAsync(EvalTask task, string hidden, CancellationToken cancellationToken) =>
        PassedShare(await tests.RunAsync(task.TestsProjectFile, Path.GetFileNameWithoutExtension(hidden), cancellationToken));

    private static double PassedShare(TestReport report) => report.Total == 0 ? 0 : (double)report.Passed / report.Total;

    // Comments don't count: mentioning the forbidden text ("this used to be double") isn't using it.
    private string? FindForbidden(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => regex.IsMatch(Comment().Replace(File.ReadAllText(file), string.Empty)))
            .Select(file => Path.GetRelativePath(folder, file))
            .FirstOrDefault();
    }

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
