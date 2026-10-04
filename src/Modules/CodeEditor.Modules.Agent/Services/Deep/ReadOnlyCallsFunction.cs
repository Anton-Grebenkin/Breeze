using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Deep;

/// <summary>
/// An approval-required tool for the explorer (ADR 0012): the explorer has no cards, so it runs only the calls the
/// owning module considers reads (<c>git log</c>, file listing) and rejects the rest with an explanation.
/// Does not delegate <c>GetService</c>: the explorer's tool loop must not see the approval marker and wait for a card.
/// </summary>
internal sealed class ReadOnlyCallsFunction(AIFunction inner, IAgentApprovalPolicy policy) : AIFunction
{
    public override string Name => inner.Name;

    public override string Description => inner.Description;

    public override JsonElement JsonSchema => inner.JsonSchema;

    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
        policy.IsReadOnly(Name, arguments)
            ? inner.InvokeAsync(arguments, cancellationToken)
            : ValueTask.FromResult<object?>(Strings.ExplorerReadOnlyCommands);
}
