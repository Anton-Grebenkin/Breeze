namespace CodeEditor.Agent.Eval;

/// <summary>Result of one task: whether it was solved, why not, how close it came and what it cost.</summary>
internal sealed record EvalResult(string Model, string Mode, string Task, bool Solved, string Details)
{
    /// <summary>Model service (<see cref="EvalOptions.Service"/>); ProxyAPI for runs saved before the option.</summary>
    public string Service { get; init; } = CodeEditor.Modules.Agent.Services.Api.AgentServices.ProxyApi.Title;

    public const string TurnErrorPrefix = "ошибка хода: ";

    /// <summary>Turn error (request rejected, model unavailable); <c>null</c> when the turn had none.</summary>
    public string? TurnError { get; init; }

    /// <summary>Share of hidden tests passed, 0 to 1; <c>null</c> when the task has none.</summary>
    public double? Score { get; init; }

    /// <summary>Number of tests the agent added to the test project.</summary>
    public int TestsAdded { get; init; }

    /// <summary>Requests to the main model during the turn.</summary>
    public int Requests { get; init; }

    public int ToolCalls { get; init; }

    /// <summary>All chat input tokens, including helper models.</summary>
    public long InputTokens { get; init; }

    public long OutputTokens { get; init; }

    /// <summary>Input tokens read from the cache (cheaper than regular input).</summary>
    public long CachedInputTokens { get; init; }

    /// <summary>Cost in rubles; <c>null</c> when the model price is unknown.</summary>
    public decimal? Cost => ModelPrice.For(Model, Service)?.Cost(InputTokens, CachedInputTokens, OutputTokens);

    /// <summary>No automatic check: the answer is reviewed by hand and excluded from the solved share.</summary>
    public bool Manual { get; init; }

    public TimeSpan Elapsed { get; init; }

    /// <summary>Duration of the follow-up request (<see cref="EvalTask.FollowUp"/>).</summary>
    public TimeSpan? FollowUpElapsed { get; init; }

    /// <summary>The turn hit the mode's step limit.</summary>
    public bool HitStepLimit { get; init; }

    /// <summary>Run folder: the repository after the turn and the chat history in <c>.breeze/agent</c>.</summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>
    /// Applies the check result. A turn error comes first; otherwise the failure would look like "0 requests".
    /// </summary>
    public EvalResult WithVerdict(EvalVerdict verdict, bool manual)
    {
        List<string> problems = [.. TurnError is null ? [] : new[] { TurnErrorPrefix + TurnError }, .. verdict.Problems];
        return this with
        {
            Manual = manual,
            Score = verdict.Score,
            Solved = !manual && problems.Count == 0,
            Details = manual ? "оценить вручную" : problems.Count == 0 ? "решена" : string.Join("; ", problems),
        };
    }
}
