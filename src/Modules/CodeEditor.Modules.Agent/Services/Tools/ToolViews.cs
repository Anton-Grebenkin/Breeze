using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Feed line for a tool call, built by module presenters (<see cref="IAgentToolPresenter"/>); a tool without one gets
/// a fallback "name(arguments)" line. A failure (a tool exception or a refusal with the error prefix,
/// <see cref="GuardedToolFunction"/>) marks the line even if the module does not know about it.
/// </summary>
public sealed partial class ToolViews(IEnumerable<IAgentToolPresenter> presenters, ILogger<ToolViews> logger)
{
    public static string ErrorPrefix => Strings.ToolErrorPrefix;

    /// <param name="result"><c>null</c> while the tool is still running.</param>
    public AgentToolView Describe(FunctionCallContent call, FunctionResultContent? result = null)
    {
        ArgumentNullException.ThrowIfNull(call);
        var text = result is null ? null : ResultText(result);
        var request = new AgentToolCall(call.Name, call.Arguments is { } arguments ? new Dictionary<string, object?>(arguments) : [], text);
        var view = presenters.Select(presenter => TryPresent(presenter, request)).FirstOrDefault(found => found is not null)
            ?? new AgentToolView(AgentToolIcon.Other, ToolCallText.Describe(call));
        return result is not null && IsFailure(result, text) ? view with { IsFailure = true } : view;
    }

    private static bool IsFailure(FunctionResultContent result, string? text) =>
        result.Exception is not null || (text?.StartsWith(ErrorPrefix, StringComparison.Ordinal) ?? false);

    // AIFunctionFactory tools return a string wrapped in a JsonElement.
    private static string ResultText(FunctionResultContent result) => result.Result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        var other => other.ToString() ?? string.Empty,
    };

    // A presenter failure must not break the turn: the fallback line is used.
    private AgentToolView? TryPresent(IAgentToolPresenter presenter, AgentToolCall call)
    {
        try
        {
            return presenter.Present(call);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPresenterFailed(logger, exception, call.Name, presenter.GetType().Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool row for {Tool} from {Presenter} was not built")]
    private static partial void LogPresenterFailed(ILogger logger, Exception exception, string tool, string presenter);
}
