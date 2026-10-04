namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>How to enable Claude reasoning: without an explicit parameter it does not reason via ProxyAPI at all (ADR 0010).</summary>
public enum ClaudeThinking
{
    /// <summary>Not Claude: no parameter is sent.</summary>
    None,

    /// <summary>Claude 5+: the model decides how much to think; a summary is requested, otherwise the reasoning text is empty.</summary>
    Adaptive,

    /// <summary>Claude 4.x: rejects "adaptive" (400) and needs a token budget.</summary>
    Budget,
}
