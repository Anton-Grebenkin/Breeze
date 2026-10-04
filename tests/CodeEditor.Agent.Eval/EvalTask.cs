namespace CodeEditor.Agent.Eval;

/// <summary>
/// Benchmark task: the user request and how to check the result. <see cref="Seeds"/> plant the task's bug in a clean
/// repository; the model never sees the hidden tests, which are added to the test project only for the check.
/// </summary>
internal sealed record EvalTask(string Id, string Prompt)
{
    /// <summary>Sample repository in <c>Fixtures</c>; its test project is <c>tests/&lt;name&gt;.Tests</c>.</summary>
    public string Fixture { get; init; } = "Calc";

    /// <summary>External git repository (full path) used instead of <c>Fixtures</c>; the run works on a clone.</summary>
    public string? SourceRepo { get; init; }

    /// <summary>Test project folder when it isn't <c>tests/&lt;Fixture&gt;.Tests</c>.</summary>
    public string? TestsProjectOverride { get; init; }

    /// <summary>Test project file when it isn't <c>&lt;Fixture&gt;.Tests.csproj</c>.</summary>
    public string? TestsProjectFileOverride { get; init; }

    /// <summary>Folder for hidden tests inside the repository; the test project by default.</summary>
    public string? HiddenTarget { get; init; }

    /// <summary>
    /// Runs only the hidden tests: a full run of a large external project means tens of minutes of unrelated
    /// integration tests.
    /// </summary>
    public bool HiddenOnly { get; init; }

    public string TestsProject => TestsProjectOverride ?? $"tests/{Fixture}.Tests";

    public string TestsProjectFile => TestsProjectFileOverride ?? $"{TestsProject}/{Fixture}.Tests.csproj";

    /// <summary>Replacements in repository files before the turn: (relative path, from, to).</summary>
    public IReadOnlyList<(string File, string From, string To)> Seeds { get; init; } = [];

    /// <summary>
    /// File in <c>Hidden</c> (or a full path for an external task) with tests that must pass after the turn; the share
    /// passed is the task's score.
    /// </summary>
    public string? HiddenTests { get; init; }

    /// <summary>New files written before the turn: folder rules, tools (relative path, text).</summary>
    public IReadOnlyList<(string File, string Text)> Files { get; init; } = [];

    /// <summary>Text the answer must contain (a question without edits).</summary>
    public string? AnswerMustContain { get; init; }

    /// <summary>Files the agent must not change (e.g. a failing test).</summary>
    public IReadOnlyList<string> Unchanged { get; init; } = [];

    /// <summary>Regular expression that must not remain in .cs files.</summary>
    public string? Forbidden { get; init; }

    /// <summary>The answer has no automatic check and is read by a person (e.g. a project overview).</summary>
    public bool ManualReview { get; init; }

    /// <summary>
    /// Second request in the same chat: the first goes in Ask mode, then the mode switches to the run's mode and this
    /// one is sent, like a user asking "what's the cause?", switching to Agent and saying "make that change".
    /// </summary>
    public string? FollowUp { get; init; }

    /// <summary>A task with edits, checked by a build and tests.</summary>
    public bool ChangesCode => AnswerMustContain is null && !ManualReview;
}
