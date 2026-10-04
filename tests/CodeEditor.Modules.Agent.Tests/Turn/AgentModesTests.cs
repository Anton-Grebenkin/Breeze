using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.Services.Tools;
using CodeEditor.Modules.Agent.Services.Turn;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace CodeEditor.Modules.Agent.Tests.Turn;

/// <summary>Modes (ADR 0010): reading settings, including legacy values, and tool access (ADR 0012).</summary>
public sealed class AgentModesTests
{
    [Theory]
    [InlineData("agent", AgentMode.Agent)]
    [InlineData("deep", AgentMode.Deep)]
    [InlineData("ask", AgentMode.Ask)]
    [InlineData("Plan", AgentMode.Plan)]
    [InlineData("light", AgentMode.Agent)]
    [InlineData("medium", AgentMode.Agent)]
    [InlineData("power", AgentMode.Agent)]
    public void Setting_IsRead_IncludingLegacyModes(string value, AgentMode expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("agent:mode", value)])
            .Build();

        var options = configuration.GetSection(AgentOptions.Section).Get<AgentOptions>();

        Assert.Equal(expected, options!.Mode);
    }

    [Fact]
    public void SettingValue_IsLowerCase() =>
        Assert.Equal(["agent", "deep", "ask", "plan"], AgentModes.All.Select(AgentModes.SettingValue));

    [Theory]
    [InlineData(AgentMode.Agent, "apply_edits", false, true)]
    [InlineData(AgentMode.Ask, "apply_edits", false, false)]
    [InlineData(AgentMode.Ask, "read_file", true, true)]
    [InlineData(AgentMode.Ask, WorkflowAgentTools.AskUserName, false, false)]
    [InlineData(AgentMode.Plan, WorkflowAgentTools.AskUserName, false, true)]
    [InlineData(AgentMode.Plan, "run_command", false, false)]
    public void Policy_AllowsReadOnlyToolsInAskAndPlan(AgentMode mode, string name, bool readOnly, bool expected)
    {
        var tool = AIFunctionFactory.Create(() => "ok", name);

        Assert.Equal(expected, ToolPolicy.Allows(mode, readOnly ? new ReadOnlyAIFunction(tool) : tool));
    }

    [Fact]
    public void ReadOnlyMark_IsFoundThroughWrappers()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "read_file");

        Assert.True(ReadOnlyAIFunction.IsReadOnly(new ApprovalRequiredAIFunction(new ReadOnlyAIFunction(tool))));
        Assert.False(ReadOnlyAIFunction.IsReadOnly(new ApprovalRequiredAIFunction(tool)));
    }
}
