using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Feed;
using CodeEditor.Modules.Terminal.Resources;
using CodeEditor.Modules.Terminal.Services.Build;
using CodeEditor.Modules.Terminal.Services.Commands;

namespace CodeEditor.Modules.Terminal.Services;

/// <summary>
/// Build, test and command rows in the agent feed (ADR 0010), e.g. "Build · succeeded", "Tests "Order" · 12 of 12
/// passed", "Command git status · exit 0". The detail is parsed from the report's first line
/// (<see cref="BuildReportText"/>, <see cref="TestReportText"/>, <see cref="CommandAgentTools"/> output) using the
/// same resource formats; failures mark the row.
/// </summary>
public sealed class TerminalToolPresenter : IAgentToolPresenter
{
    private const int MaxCommandLength = 60;

    public AgentToolView? Present(AgentToolCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Name switch
        {
            BuildAgentTools.BuildName => Build(call),
            BuildAgentTools.RunTestsName => Tests(call),
            BuildAgentTools.GetErrorsName => new AgentToolView(AgentToolIcon.Build, Strings.LastBuildErrors) { IsExploration = true },
            CommandAgentTools.RunCommandName => Command(call),
            BackgroundCommandTools.CommandOutputName => Background(call, Strings.BackgroundOutputTitle),
            BackgroundCommandTools.StopCommandName => Background(call, Strings.BackgroundStopTitle),
            _ => null,
        };
    }

    // "Background command #2 output · running" or "… · exit 0": state comes from the report's first line.
    private static AgentToolView Background(AgentToolCall call, string title)
    {
        var view = new AgentToolView(AgentToolIcon.Terminal, Format(title, call.Text("id") ?? "?")) { IsExploration = true };
        return FirstLine(call.Result) is { } line && BackgroundState(line) is { } state
            ? view with { Detail = state.Detail, IsFailure = state.IsFailure }
            : view;
    }

    private static (string Detail, bool IsFailure)? BackgroundState(string line)
    {
        if (ResourcePattern.Match(Strings.BackgroundRunning, line) is not null)
        {
            return (Strings.DetailRunning, false);
        }

        if (ResourcePattern.Match(Strings.BackgroundStopped, line) is not null)
        {
            return (Strings.DetailStopped, false);
        }

        return ResourcePattern.Match(Strings.BackgroundExited, line) is { } exited
            ? (Format(Strings.DetailExitCode, exited[1]), exited[1] != "0")
            : null;
    }

    private static AgentToolView Build(AgentToolCall call)
    {
        var view = new AgentToolView(AgentToolIcon.Build, call.IsDone ? Strings.Build : Strings.Building);
        if (FirstLine(call.Result) is not { } line)
        {
            return view;
        }

        if (ResourcePattern.Match(Strings.BuildSucceededWithWarnings, line) is { } warned)
        {
            return view with { Detail = Format(Strings.DetailSucceededWithWarnings, warned[2]) };
        }

        if (ResourcePattern.Match(Strings.BuildSucceeded, line) is not null)
        {
            return view with { Detail = Strings.DetailSucceeded };
        }

        var detail = ResourcePattern.Match(Strings.BuildTimedOut, line) is not null
            ? Strings.DetailTimedOut
            : Format(Strings.DetailErrors, ResourcePattern.Match(Strings.BuildFailed, line)?[2] ?? "0");
        return view with { Detail = detail, IsFailure = true };
    }

    private static AgentToolView Tests(AgentToolCall call)
    {
        var filter = call.Text("filter");
        var view = new AgentToolView(AgentToolIcon.Test, filter is null ? Strings.Tests : Format(Strings.TestsWithFilter, filter));
        if (FirstLine(call.Result) is not { } line)
        {
            return view;
        }

        if (ResourcePattern.Match(Strings.TestsSummary, line) is not { } totals)
        {
            var reason = ResourcePattern.Match(Strings.TestsTimedOut, line) is not null ? Strings.DetailTimedOut
                : ResourcePattern.Match(Strings.TestsNotStarted, line) is not null ? Strings.DetailNotBuilt
                : Strings.DetailNoTotals;
            return view with { Detail = reason, IsFailure = true };
        }

        var (passed, total, failed) = (totals[1], totals[2], totals[3]);
        return failed == "0"
            ? view with { Detail = Format(Strings.DetailPassed, passed, total) }
            : view with { Detail = Format(Strings.DetailFailed, failed, total), IsFailure = true };
    }

    private static AgentToolView Command(AgentToolCall call)
    {
        var command = call.Text("command") ?? "?";
        var shown = command.Length <= MaxCommandLength ? command : string.Concat(command.AsSpan(0, MaxCommandLength), "…");
        var view = new AgentToolView(AgentToolIcon.Terminal, Format(Strings.CommandTitle, shown));
        if (FirstLine(call.Result) is not { } line)
        {
            return view;
        }

        // Background start: "background #2" while running; the exit code if it failed right away.
        if (ResourcePattern.Match(Strings.BackgroundRunning, line) is { } running)
        {
            return view with { Detail = Format(Strings.DetailBackground, running[0]) };
        }

        if (BackgroundState(line) is { } finished)
        {
            return view with { Detail = finished.Detail, IsFailure = finished.IsFailure };
        }

        if (ResourcePattern.Match(Strings.CommandExitCode, line) is not { } exit
            || !int.TryParse(exit[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out var code))
        {
            return view with { Detail = ResourcePattern.Match(Strings.CommandTimedOut, line) is not null ? Strings.DetailTimedOut : null, IsFailure = true };
        }

        return view with { Detail = Format(Strings.DetailExitCode, code), IsFailure = code != 0 };
    }

    // Slices instead of Split: Split would also copy the whole remainder of a long output.
    private static string? FirstLine(string? result)
    {
        if (result is null)
        {
            return null;
        }

        var end = result.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? result : result[..end];
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
