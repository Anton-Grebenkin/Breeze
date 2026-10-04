using CodeEditor.Modules.Agent.Contracts.Approvals;

namespace CodeEditor.Modules.Docker.Services.Agent;

/// <summary>
/// Approval of Docker changes (ADR 0028): every <c>docker_change</c> action goes through a card, and "Always allow"
/// stores the action in <c>docker.alwaysAllow</c> (<see cref="ActionApprovalPolicy"/>). exec and rm always ask: a
/// command in a container can do anything, and removal cannot be undone.
/// </summary>
public static class DockerApprovals
{
    public const string AlwaysAllowKey = DockerOptions.Section + ".alwaysAllow";

    public static ActionApprovalRules Rules { get; } =
        new(DockerAgentTools.ChangeName, AlwaysAllowKey, DockerCommands.ChangeActions, [DockerCommands.Exec, DockerCommands.Remove]);
}
