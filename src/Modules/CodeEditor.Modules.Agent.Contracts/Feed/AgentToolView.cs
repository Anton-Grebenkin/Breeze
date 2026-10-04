namespace CodeEditor.Modules.Agent.Contracts.Feed;

/// <summary>
/// How a tool call looks in the feed (ADR 0010, like Copilot's <c>invocationMessage</c> / <c>pastTenseMessage</c>): an
/// icon and a title ("Reading Order.cs" while running, "Read Order.cs" afterwards), with a detail on the right.
/// </summary>
/// <param name="Title">Verb and object: "Searching "Reserve"", "Edited Order.cs".</param>
public sealed record AgentToolView(AgentToolIcon Icon, string Title)
{
    /// <summary>Result detail: "lines 1–80", "5 matches in 2 files", "+12 −3"; <c>null</c> for none.</summary>
    public string? Detail { get; init; }

    /// <summary>File opened by clicking the row (relative to the folder); <c>null</c> for none.</summary>
    public string? FilePath { get; init; }

    /// <summary>Exploration (read, search, view): the feed collapses consecutive ones into one "Explored: …" row.</summary>
    public bool IsExploration { get; init; }

    /// <summary>The tool reported a failure (build errors, failing tests, refusal).</summary>
    public bool IsFailure { get; init; }
}
