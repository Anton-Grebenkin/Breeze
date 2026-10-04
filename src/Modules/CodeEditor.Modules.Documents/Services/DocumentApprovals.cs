using System.Text.Json;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Contracts.Approvals;
using CodeEditor.Modules.Documents.Services.Changes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Documents.Services;

/// <summary>
/// Approvals for document edits: every <c>document_change</c> action goes through a card; "Always allow" stores the
/// action in <c>documents.alwaysAllow</c> (<see cref="ActionApprovalPolicy"/>). Replacing a whole existing file
/// (<c>overwrite: true</c>) always asks: the previous document is replaced as a whole.
/// </summary>
public sealed class DocumentApprovals : IAgentApprovalPolicy
{
    public const string AlwaysAllowKey = DocumentOptions.Section + ".alwaysAllow";

    private readonly ActionApprovalPolicy _actions;

    public DocumentApprovals(IOptionsMonitor<DocumentOptions> options, ISettingsService settings, ILogger<ActionApprovalPolicy> logger) =>
        _actions = new ActionApprovalPolicy(Rules, () => options.CurrentValue.AlwaysAllow, settings, logger);

    public static ActionApprovalRules Rules { get; } = new(DocumentAgentTools.ChangeName, AlwaysAllowKey, DocumentActions.All, []);

    public bool CanDecide(string toolName) => _actions.CanDecide(toolName);

    public bool IsPreapproved(string toolName, IDictionary<string, object?> arguments) =>
        !Overwrites(arguments) && _actions.IsPreapproved(toolName, arguments);

    public bool IsReadOnly(string toolName, IDictionary<string, object?> arguments) => false;

    public string? SuggestRule(string toolName, IDictionary<string, object?> arguments) =>
        Overwrites(arguments) ? null : _actions.SuggestRule(toolName, arguments);

    public void AllowAlways(string toolName, string rule) => _actions.AllowAlways(toolName, rule);

    public bool AlwaysAsks(string toolName, IDictionary<string, object?> arguments) => CanDecide(toolName) && Overwrites(arguments);

    private static bool Overwrites(IDictionary<string, object?> arguments) =>
        arguments.TryGetValue("overwrite", out var value) && value is true or JsonElement { ValueKind: JsonValueKind.True };
}
