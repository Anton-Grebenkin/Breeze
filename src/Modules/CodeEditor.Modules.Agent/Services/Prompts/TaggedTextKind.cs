namespace CodeEditor.Modules.Agent.Services.Prompts;

public enum TaggedTextKind
{
    /// <summary>Answer text or a turn line.</summary>
    Text,

    /// <summary>Reasoning aloud inside the tag.</summary>
    Thinking,

    /// <summary>The reasoning tag closed: further reasoning is a new step.</summary>
    ThinkingDone,
}
