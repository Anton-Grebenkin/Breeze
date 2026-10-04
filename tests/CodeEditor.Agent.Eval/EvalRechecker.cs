using CodeEditor.Core.Files;
using CodeEditor.Modules.Terminal.Services.Build;
using Microsoft.Extensions.DependencyInjection;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Re-verifies a past run without a model after hidden tests or harness checks are fixed: the repository in the run
/// folder is checked again and the verdict, score and tests added are recomputed; turn metrics and the turn error stay.
/// Question tasks are skipped because the agent's answer isn't stored in the results.
/// </summary>
internal static class EvalRechecker
{
    public static async Task<EvalResult> RecheckAsync(EvalResult result, EvalTask task, CancellationToken cancellationToken)
    {
        if (!task.ChangesCode)
        {
            return result;
        }

        var repo = Path.Combine(result.Folder, "repo");
        if (task.HiddenTests is { } hidden)
        {
            File.Delete(Path.Combine(repo, task.TestsProject, hidden));
        }

        // A clean copy provides the protected files and the test count before the turn.
        var clean = EvalRepository.Create(Path.Combine(Path.GetTempPath(), "CodeEditor.Eval", "recheck-" + Guid.NewGuid().ToString("N")), task);
        try
        {
            var originals = task.Unchanged.ToDictionary(file => file, file => File.ReadAllText(Path.Combine(clean, file)), StringComparer.Ordinal);
            var testsAdded = EvalRepository.CountTests(repo, task) - EvalRepository.CountTests(clean, task);
            var verdict = await VerifyAsync(result, task, repo, originals, cancellationToken);
            return WithLegacyTurnError(result).WithVerdict(verdict, task.ManualReview) with { TestsAdded = testsAdded };
        }
        finally
        {
            Directory.Delete(clean, recursive: true);
        }
    }

    private static async Task<EvalVerdict> VerifyAsync(EvalResult result, EvalTask task, string repo, Dictionary<string, string> originals, CancellationToken cancellationToken)
    {
        using var dispatcher = new EvalDispatcher();
        await using var services = EvalHost.Build(Path.Combine(result.Folder, "user"), dispatcher, "recheck", dryRun: true);
        await dispatcher.InvokeAsync(() => services.GetRequiredService<IWorkspace>().Open(repo));
        await services.GetRequiredService<IFileIndex>().WhenReady;
        return await new EvalVerifier(services.GetRequiredService<DotNetBuild>(), services.GetRequiredService<DotNetTests>(), repo)
            .VerifyAsync(task, string.Empty, originals, cancellationToken);
    }

    // Results saved before TurnError existed kept the turn error only as the first reason in Details.
    private static EvalResult WithLegacyTurnError(EvalResult result) =>
        result.TurnError is null && result.Details.StartsWith(EvalResult.TurnErrorPrefix, StringComparison.Ordinal)
            ? result with { TurnError = result.Details[EvalResult.TurnErrorPrefix.Length..].Split("; ")[0] }
            : result;
}
