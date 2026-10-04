namespace CodeEditor.Modules.Agent.Contracts.Feed;

/// <summary>Icon of a tool row in the agent feed: what the call does.</summary>
public enum AgentToolIcon
{
    Read,
    Search,
    Folder,
    Edit,
    Create,
    Delete,
    Move,
    Build,
    Test,
    Terminal,
    Plan,
    Question,
    Diff,

    /// <summary>Folder memory: remember, read, forget.</summary>
    Memory,

    /// <summary>Code exploration by a subagent.</summary>
    Explore,

    /// <summary>Git repository: status, history, commit.</summary>
    Git,

    /// <summary>Internet: a page, search.</summary>
    Web,

    /// <summary>Docker containers: status, logs, build, run.</summary>
    Container,

    /// <summary>An image from the folder: screenshot, mockup, diagram.</summary>
    Image,
    Other,
}
