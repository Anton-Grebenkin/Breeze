using CodeEditor.Modules.Agent.Services.Cache;
using CodeEditor.Modules.Agent.Services.Settings;
using CodeEditor.Modules.Agent.ViewModels.Composer;
using CodeEditor.Testing;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Tests.Cache;

/// <summary>
/// Prompt cache (ADR 0012): prefix changes and misses go to the log, the cached input share to the context details.
/// </summary>
public sealed class PromptCacheMonitorTests
{
    private readonly CollectingLogger<PromptCacheMonitorTests> _log = new();

    [Fact]
    public void SamePrefix_IsQuiet_ChangedInstructionsAndTools_AreLogged()
    {
        var monitor = new PromptCacheMonitor(_log);
        var read = AIFunctionFactory.Create(() => "ok", "read_file");

        monitor.BeforeRequest(new ChatOptions { Instructions = "промпт", Tools = [read] });
        monitor.BeforeRequest(new ChatOptions { Instructions = "промпт", Tools = [read] });
        Assert.Empty(_log.Entries);

        monitor.BeforeRequest(new ChatOptions { Instructions = "другой промпт", Tools = [read] });
        monitor.BeforeRequest(new ChatOptions { Instructions = "другой промпт", Tools = [read, AIFunctionFactory.Create(() => "ok", "build")] });

        Assert.Equal(2, _log.Entries.Count);
        Assert.Contains("(instructions)", _log.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("(tools)", _log.Entries[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_StartsNewChat_WithoutWarning()
    {
        var monitor = new PromptCacheMonitor(_log);
        monitor.BeforeRequest(new ChatOptions { Instructions = "чат 1" });

        monitor.Reset();
        monitor.BeforeRequest(new ChatOptions { Instructions = "чат 2" });

        Assert.Empty(_log.Entries);
    }

    [Theory]
    [InlineData(10_000, 12_000, 9_500, false)]
    [InlineData(10_000, 12_000, 1_000, true)]
    [InlineData(0, 12_000, 0, false)]
    [InlineData(3_000, 3_500, 0, true)]
    [InlineData(1_500, 1_900, 0, false)]
    public void Miss_LessThanHalfOfPreviousInputFromCache_AndNoticeablyMuchReprocessed(long expected, long input, long cached, bool miss) =>
        Assert.Equal(miss, PromptCacheMonitor.IsMiss(expected, input, cached));

    [Fact]
    public void AfterResponse_ComparesWithPreviousRequest_AndSkipsUnreportedCache()
    {
        var monitor = new PromptCacheMonitor(_log);

        monitor.AfterResponse(20_000, 0);
        monitor.AfterResponse(25_000, 19_500);
        monitor.AfterResponse(26_000, cached: null);
        Assert.Empty(_log.Entries);

        monitor.AfterResponse(30_000, 1_000);

        var entry = Assert.Single(_log.Entries);
        Assert.Contains("cache miss: 29000 of 30000", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextDetails_ShowCacheShareOfChatInput()
    {
        var usage = new ContextUsageViewModel(new TestOptionsMonitor<AgentOptions>(new AgentOptions()));

        usage.Add(10_000, 100, 0);
        usage.Add(30_000, 100, 20_000);

        Assert.Equal(0.5, usage.CacheShare, 3);
        Assert.Contains("из кэша 50% ввода", usage.Spent, StringComparison.Ordinal);
    }
}
