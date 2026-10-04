using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Modules.Agent.Rules;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Turn;

/// <summary>
/// Agent tool wrapper:
/// - logs the call and its duration; returns <see cref="AgentToolException"/> as text so the model can fix the call;
/// - refuses a tool not available in the turn's mode and tells the model why (<see cref="ToolPolicy"/>);
/// - runs nothing once the turn budget is exhausted;
/// - marks an identical call the second time and blocks it from the third (loops); a successful edit resets the count;
/// - after a long run of reads without edits or checks, appends a request for an interim summary;
/// - runs mutating tools (those needing approval) one at a time, reads in parallel;
/// - records calls that bring external content in the turn budget: no memory is extracted from such a turn;
/// - in Deep mode, appends the advisor's advice when the model looks stuck (<see cref="DeepSupervisor"/>);
/// - if the user wrote while the agent worked (<see cref="UserMessageQueue"/>), ends the tool loop after the current
///   calls so the message reaches the model;
/// - the first read of a file with a rule in .breeze/rules brings the rule text (<see cref="ProjectRuleGuard"/>).
/// </summary>
internal sealed partial class GuardedToolFunction(
    AIFunction inner, TurnBudget budget, SemaphoreSlim writeLock, ILogger logger, DeepSupervisor? deep = null, UserMessageQueue? queue = null, ProjectRuleGuard? rules = null)
    : DelegatingAIFunction(inner)
{
    public static string ExhaustedResult => Strings.ToolBudgetExhausted;

    public static string RepeatNote => "\n\n" + Strings.ToolRepeatNote;

    public static string BlockedResult => Strings.ToolRepeatBlocked;

    public static string ExplorationNote => "\n\n" + string.Format(CultureInfo.CurrentCulture, Strings.ExplorationStallNote, TurnBudget.ReadsBeforeReflection);

    private readonly bool _mutating = inner.GetService<ApprovalRequiredAIFunction>() is not null;

    /// <summary>Reply to the model for a tool not available in the mode; nothing was run.</summary>
    public static string Refusal(string tool, AgentMode mode) =>
        string.Format(CultureInfo.CurrentCulture, Strings.ToolUnavailableInMode, tool, AgentModes.Title(mode));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!ToolPolicy.Allows(budget.Mode, this))
        {
            LogRefused(logger, Name, budget.Mode);
            return Refusal(Name, budget.Mode);
        }

        if (budget.IsExhausted)
        {
            return ExhaustedResult;
        }

        foreach (var name in ToolArgumentRepair.Repair(arguments, JsonSchema))
        {
            LogArgumentRepaired(logger, Name, name);
        }

        var repeat = budget.RegisterCall(Signature(arguments));
        if (repeat >= TurnBudget.BlockedRepeat)
        {
            LogRepeatBlocked(logger, Name, repeat);
            return BlockedResult;
        }

        var result = await InvokeInOrderAsync(arguments, cancellationToken);
        if (ExternalContent.Has(this))
        {
            budget.RegisterExternalContent();
        }

        if (_mutating && !IsError(result))
        {
            // The folder changed: a build, test or read after an edit is a new result, not a loop.
            budget.ForgetCalls();
        }

        result = WithFileRules(arguments, result);
        if (deep is not null && AsText(result) is { } output && await deep.AfterToolAsync(Name, output, cancellationToken) is { } advice)
        {
            result = output + "\n\n" + advice;
        }

        if (repeat > 1 && AsText(result) is { } text)
        {
            result = text + RepeatNote;
        }

        YieldToUser();
        return AfterExploration(result);
    }

    // A user message is waiting: these results go to history, and the next model request carries the message.
    private void YieldToUser()
    {
        if (queue?.HasPending == true && FunctionInvokingChatClient.CurrentContext is { } current)
        {
            current.Terminate = true;
        }
    }

    private object? WithFileRules(AIFunctionArguments arguments, object? result) =>
        rules is not null && Name == ProjectRuleGuard.ReadFileTool && !IsError(result) && AsText(result) is { } text
            && arguments.TryGetValue("path", out var path) && path?.ToString() is { Length: > 0 } file && rules.RulesForRead(file) is { } note
            ? text + note
            : result;

    // Reflection on a signal, not on a schedule (ADR 0016), in every mode: models can search and read dozens of times
    // in a row. Not for the explorer subagent: reading is its job.
    private object? AfterExploration(object? result)
    {
        if (!budget.ReflectsOnExploration)
        {
            return result;
        }

        if (!ReadOnlyAIFunction.IsReadOnly(this))
        {
            budget.RegisterProgress();
            return result;
        }

        return budget.RegisterRead() && AsText(result) is { } text ? text + ExplorationNote : result;
    }

    private async Task<object?> InvokeInOrderAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!_mutating)
        {
            return await InvokeLoggedAsync(arguments, cancellationToken);
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            return await InvokeLoggedAsync(arguments, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task<object?> InvokeLoggedAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            LogCalled(logger, Name, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (AgentToolException exception)
        {
            LogRejected(logger, Name, exception.Message);
            return $"{ToolViews.ErrorPrefix} {exception.Message}";
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidCastException)
        {
            // Arguments did not bind; without an explanation the model gets "Function failed" and works around the tool.
            LogRejected(logger, Name, exception.Message);
            return $"{ToolViews.ErrorPrefix} {string.Format(CultureInfo.CurrentCulture, Strings.ToolArgumentsInvalid, exception.Message, Parameters())}";
        }
        catch (Exception exception) when (exception is not OperationCanceledException && LogFailed(exception))
        {
            throw;
        }
    }

    // Parameters from the schema: "edits: array, path: string".
    private string Parameters() =>
        JsonSchema.ValueKind == JsonValueKind.Object && JsonSchema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object
            ? string.Join(", ", properties.EnumerateObject().Select(static property => $"{property.Name}: {(property.Value.TryGetProperty("type", out var type) ? TypeName(type) : "any")}"))
            : string.Empty;

    // An optional parameter's type is ["string", "null"]; "string|null" is clearer to the model.
    private static string TypeName(JsonElement type) =>
        type.ValueKind == JsonValueKind.Array ? string.Join('|', type.EnumerateArray().Select(static item => item.ToString())) : type.ToString();

    private static bool IsError(object? result) => AsText(result) is { } text && text.StartsWith(ToolViews.ErrorPrefix, StringComparison.Ordinal);

    // AIFunctionFactory tools return a string wrapped in a JsonElement.
    private static string? AsText(object? result) => result switch
    {
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    // Arguments sorted by name: the order the model sent them in does not make a different call.
    private string Signature(AIFunctionArguments arguments) =>
        Name + JsonSerializer.Serialize(arguments.OrderBy(static argument => argument.Key, StringComparer.Ordinal).ToDictionary(), AIJsonUtilities.DefaultOptions);

    // Exception filter: logs without catching, so the stack and outer handling stay intact.
    private bool LogFailed(Exception exception)
    {
        LogCrashed(logger, exception, Name, exception.GetType().Name);
        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent called {Tool} ({ElapsedMs} ms)")]
    private static partial void LogCalled(ILogger logger, string tool, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent called {Tool}: {Reason}")]
    private static partial void LogRejected(ILogger logger, string tool, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tool {Tool} crashed: {Error}")]
    private static partial void LogCrashed(ILogger logger, Exception exception, string tool, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Repeated call to {Tool} blocked (identical call #{Count} in the turn)")]
    private static partial void LogRepeatBlocked(ILogger logger, string tool, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool {Tool}: argument {Argument} came as a JSON string and was parsed")]
    private static partial void LogArgumentRepaired(ILogger logger, string tool, string argument);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool {Tool} refused in {Mode} mode")]
    private static partial void LogRefused(ILogger logger, string tool, AgentMode mode);
}
