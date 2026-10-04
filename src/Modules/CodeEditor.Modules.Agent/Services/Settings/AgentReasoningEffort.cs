namespace CodeEditor.Modules.Agent.Services.Settings;

/// <summary>How much the model reasons before answering (<c>reasoning_effort</c>); non-reasoning models ignore it.</summary>
public enum AgentReasoningEffort
{
    /// <summary>Not sent: the model decides.</summary>
    Default,
    None,
    Low,
    Medium,
    High,
    ExtraHigh,
}
