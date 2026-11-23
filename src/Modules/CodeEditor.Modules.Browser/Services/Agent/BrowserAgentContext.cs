using CodeEditor.Modules.Agent.Contracts.Context;

namespace CodeEditor.Modules.Browser.Services.Agent;

/// <summary>
/// Browser context for the agent: which sites are open, so "the page I opened" needs no explanation and the agent
/// reads it instead of asking. Sent while the user keeps the tabs chip on (<see cref="BrowserTabsSwitch"/>).
/// </summary>
public sealed class BrowserAgentContext(BrowserTabsSwitch tabs) : IAgentContextProvider
{
    public ValueTask<IReadOnlyList<string>> GetContextAsync(AgentContextRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(tabs.IsOn ? BrowserTabLines.Describe(tabs.Tabs()) : []);
}
