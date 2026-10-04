using CodeEditor.Modules.Agent.Contracts;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>Test output store: records calls and returns the text unchanged.</summary>
internal sealed class FakeOutputStore : IAgentOutputStore
{
    public List<(string Text, string Tool)> Calls { get; } = [];

    public string Fit(string text, string toolName)
    {
        Calls.Add((text, toolName));
        return text;
    }
}
