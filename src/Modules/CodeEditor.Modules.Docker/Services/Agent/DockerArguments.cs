using CodeEditor.Modules.Agent.Contracts;

namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>Reads <c>docker_change</c> arguments from the model's dictionary for the approval card.</summary>
public static class DockerArguments
{
    /// <exception cref="AgentToolException">The action is missing or an argument has the wrong type.</exception>
    public static DockerChange Change(IDictionary<string, object?> arguments) => new(ToolArguments.Get<string>(arguments, "action"))
    {
        Name = ToolArguments.GetOptional<string>(arguments, "name"),
        Image = ToolArguments.GetOptional<string>(arguments, "image"),
        Command = ToolArguments.GetOptional<string[]>(arguments, "command") ?? [],
        Ports = ToolArguments.GetOptional<string[]>(arguments, "ports") ?? [],
        Env = ToolArguments.GetOptional<string[]>(arguments, "env") ?? [],
        Volumes = ToolArguments.GetOptional<string[]>(arguments, "volumes") ?? [],
        Network = ToolArguments.GetOptional<string>(arguments, "network"),
        Detach = ToolArguments.GetOptional<bool?>(arguments, "detach") ?? true,
        Context = ToolArguments.GetOptional<string>(arguments, "context"),
        Dockerfile = ToolArguments.GetOptional<string>(arguments, "dockerfile"),
        ComposeFile = ToolArguments.GetOptional<string>(arguments, "file"),
        Services = ToolArguments.GetOptional<string[]>(arguments, "services") ?? [],
        Build = ToolArguments.GetOptional<bool>(arguments, "build"),
    };
}
