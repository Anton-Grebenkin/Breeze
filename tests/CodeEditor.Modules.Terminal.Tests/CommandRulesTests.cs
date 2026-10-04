using CodeEditor.Modules.Terminal.Services;
using CodeEditor.Modules.Terminal.Services.Commands;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Modules.Terminal.Tests;

/// <summary>User "always allow" rules (ADR 0012): word-wise prefix, rule suggestion, saving to settings.</summary>
public sealed class CommandRulesTests
{
    private readonly FakeSettingsService _settings = new();
    private readonly TestOptionsMonitor<TerminalOptions> _options = new(new TerminalOptions());

    [Theory]
    [InlineData("dotnet test", "dotnet test --no-build", true)]
    [InlineData("dotnet test", "dotnet.exe TEST", true)]
    [InlineData("dotnet test", "dotnet testx", false)]
    [InlineData("dotnet test", "dotnet", false)]
    [InlineData("npm install", "npm install lodash", true)]
    public void Rule_IsPrefixOfWords(string rule, string command, bool matches) =>
        Assert.Equal(matches, CommandRules.Matches(rule, CommandTokenizer.Parse(command).Segments[0]));

    [Theory]
    [InlineData("dotnet test --no-build; git status", true)]
    [InlineData("dotnet test && git diff", true)]
    [InlineData("dotnet build", false)]
    [InlineData("dotnet test > log.txt", false)]
    [InlineData("dotnet test C:\\other\\Tests.csproj", false)]
    public void Command_IsAllowed_WhenEveryPartIsReadOrRuled(string command, bool allowed) =>
        Assert.Equal(allowed, CommandRules.IsAllowed(CommandTokenizer.Parse(command), ["dotnet test"]));

    [Theory]
    [InlineData("dotnet test --filter Order", "dotnet test")]
    [InlineData("npm install", "npm install")]
    [InlineData("git status; git add src/A.cs", "git add")]
    [InlineData("make build", "make")]
    [InlineData("git push origin main", null)]
    [InlineData("npm run build", null)]
    [InlineData("powershell -c ls", null)]
    [InlineData("Remove-Item a.txt", null)]
    [InlineData("./build.ps1", null)]
    [InlineData("dotnet build > log.txt", null)]
    [InlineData("git status", null)]
    public void Suggestion_ForTheFirstCommandThatNeedsApproval(string command, string? rule) =>
        Assert.Equal(rule, CommandRules.Suggest(CommandTokenizer.Parse(command)));

    [Fact]
    public void Approvals_ReadOnlyWithoutAsking_RuleSavedToSettings()
    {
        var approvals = new CommandApprovals(_options, _settings, NullLogger<CommandApprovals>.Instance);
        Dictionary<string, object?> test = new() { ["command"] = "dotnet test --no-build" };

        Assert.True(approvals.IsPreapproved(CommandAgentTools.RunCommandName, new Dictionary<string, object?> { ["command"] = "git status" }));
        Assert.False(approvals.IsPreapproved(CommandAgentTools.RunCommandName, test));
        Assert.Equal("dotnet test", approvals.SuggestRule(CommandAgentTools.RunCommandName, test));

        approvals.AllowAlways(CommandAgentTools.RunCommandName, "dotnet test");

        Assert.Equal(["dotnet test"], (string[])_settings.Written[CommandApprovals.AllowedCommandsKey]!);
        _options.Set(new TerminalOptions { AllowedCommands = ["dotnet test"] });
        Assert.True(approvals.IsPreapproved(CommandAgentTools.RunCommandName, test));
        Assert.False(approvals.CanDecide("apply_edits"));
    }
}
