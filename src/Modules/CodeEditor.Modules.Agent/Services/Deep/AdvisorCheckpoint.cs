namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>Point at which Deep mode consults the advisor (ADR 0012).</summary>
public enum AdvisorCheckpoint
{
    /// <summary>Before the turn's first edit: risks of the chosen approach.</summary>
    BeforeFirstEdit,

    /// <summary>External stuck signal: a repeated failure, circling edits of one place, half the budget without success.</summary>
    Stuck,
}
