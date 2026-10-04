using System.Globalization;
using CodeEditor.Core.Threading;
using CodeEditor.Modules.Agent.Resources;
using CodeEditor.Shell.ViewModels;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Rules;

/// <summary>Problems in rule files go to the log and the status bar once, not on every read of the rules.</summary>
public sealed partial class RulesProblemReporter(StatusBarViewModel statusBar, IUiDispatcher dispatcher, ILogger<RulesProblemReporter> logger)
{
    private readonly Lock _lock = new();
    private string _last = string.Empty;

    public void Report(IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var key = string.Join('\n', problems);
        lock (_lock)
        {
            if (key == _last)
            {
                return;
            }

            _last = key;
        }

        if (problems.Count == 0)
        {
            return;
        }

        foreach (var problem in problems)
        {
            LogProblem(logger, problem);
        }

        var message = Describe(problems);
        dispatcher.Post(() => statusBar.Message = message);
    }

    /// <summary>Status bar text: the first problem and how many more.</summary>
    public static string Describe(IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.Count == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.RulesProblem, problems[0])
            : string.Format(CultureInfo.CurrentCulture, Strings.RulesProblems, problems[0], problems.Count - 1);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Agent rules: {Problem}")]
    private static partial void LogProblem(ILogger logger, string problem);
}
