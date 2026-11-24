namespace CodeEditor.Modules.Agent.Services.Probes;

/// <summary>What "Check the model" asks a model of the user's connection, in order.</summary>
public enum ModelProbeKind
{
    /// <summary>The model answers at all.</summary>
    Answer,

    /// <summary>Which field carries the response limit: <c>max_completion_tokens</c> or the older <c>max_tokens</c>.</summary>
    OutputLimit,

    /// <summary>It calls a tool: without this the agent cannot work with it.</summary>
    ToolCall,

    /// <summary>Two tool calls in one reply.</summary>
    ParallelToolCalls,

    /// <summary>Arguments with nested objects and arrays arrive whole.</summary>
    NestedArguments,

    Image,

    /// <summary>Where the model's reasoning comes from, and whether the service takes <c>reasoning_effort</c>.</summary>
    Reasoning,

    /// <summary>A question in Russian gets an answer in Russian.</summary>
    Language,
}
