using System.Text.Json;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Tasks on real repositories outside CodeEditor: a folder with <c>task.json</c>, a prompt file and hidden tests.
/// Relative paths are resolved from the task folder. The run works on a clone of <c>sourceRepo</c> and checks only
/// the hidden tests:
/// <code>
/// { "id": "shop-delete-category", "sourceRepo": "../../shop", "prompt": "prompt.md",
///   "hiddenTests": "hidden/DeleteCategoryHiddenTests.cs", "hiddenTarget": "tests/X.IntegrationTests/Tests/BenchHidden",
///   "testsProject": "tests/X.IntegrationTests", "testsProjectFile": "tests/X.IntegrationTests/X.IntegrationTests.csproj" }
/// </code>
/// </summary>
internal static class ExternalTasks
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IEnumerable<EvalTask> Load(IEnumerable<string> folders) => folders.Select(Load);

    /// <exception cref="InvalidOperationException"><c>task.json</c> is empty.</exception>
    private static EvalTask Load(string folder)
    {
        var file = Path.Combine(folder, "task.json");
        var spec = JsonSerializer.Deserialize<Spec>(File.ReadAllText(file), JsonOptions)
            ?? throw new InvalidOperationException($"{file}: пусто.");
        string Full(string path) => Path.GetFullPath(Path.Combine(folder, path));
        return new EvalTask(spec.Id, File.ReadAllText(Full(spec.Prompt)).Trim())
        {
            Fixture = spec.Id,
            SourceRepo = Full(spec.SourceRepo),
            HiddenTests = Full(spec.HiddenTests),
            HiddenTarget = spec.HiddenTarget,
            TestsProjectOverride = spec.TestsProject,
            TestsProjectFileOverride = spec.TestsProjectFile,
            HiddenOnly = true,
        };
    }

    private sealed record Spec(string Id, string SourceRepo, string Prompt, string HiddenTests, string HiddenTarget, string TestsProject, string TestsProjectFile);
}
