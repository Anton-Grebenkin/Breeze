using System.Globalization;
using CodeEditor.Modules.Agent.Contracts.Context;
using CodeEditor.Modules.Terminal.Resources;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Last build and test results in <c>&lt;context&gt;</c>, so the model sees what was verified without a call.
/// </summary>
public sealed class BuildAgentContext(DotNetBuild build, DotNetTests tests) : IAgentContextProvider
{
    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        if (build.Last is { } report)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.LastBuild, report.Finished, BuildReportText.Summary(report)));
        }

        if (tests.Last is { } tested)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Strings.LastTestRun, TestReportText.Summary(tested)));
        }

        return ValueTask.FromResult<IReadOnlyList<string>>(lines);
    }
}
