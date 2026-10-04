using CodeEditor.Modules.Agent.Services.Context;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

// Agent Framework compaction API is experimental (MAAI001), as in the agent module.
#pragma warning disable MAAI001

namespace CodeEditor.Modules.Agent.Tests.Context;

/// <summary>Token estimate corrected by real input, so compaction thresholds use model tokens, not bytes/4.</summary>
public sealed class ContextMeterTests
{
    [Fact]
    public void BeforeFirstAnswer_EstimateAsIs()
    {
        var meter = new ContextMeter();

        meter.Observed(5_000);

        Assert.Equal(40_000, meter.Tokens(40_000));
    }

    // Real log numbers: an estimate of 73K against 39K reported by the provider.
    [Fact]
    public void RatioOfRealInputToEstimate_Scales()
    {
        var meter = new ContextMeter();

        meter.Sent(73_000);
        meter.Observed(39_000);

        Assert.Equal(42_739, meter.Tokens(80_000));
    }

    [Fact]
    public void Ratio_IsClamped_AndResetByNewChat()
    {
        var meter = new ContextMeter();
        meter.Sent(1_000);
        meter.Observed(20_000);
        Assert.Equal(15_000, meter.Tokens(10_000));

        meter.Observed(10);
        Assert.Equal(2_500, meter.Tokens(10_000));

        meter.Reset();
        Assert.Equal(10_000, meter.Tokens(10_000));
    }

    // A screenshot of hundreds of KB counts as ~1.5K tokens for compaction, not ~100K (bytes/4).
    [Fact]
    public async Task Image_CountsAsFixedTokens_NotItsBytes()
    {
        var probe = new EstimateProbe();
        ChatMessage[] messages =
        [
            new(ChatRole.User, [new TextContent("смотри"), new DataContent(new byte[400_000], "image/png")]),
            new(ChatRole.Assistant, "вижу"),
            new(ChatRole.User, "дальше"),
        ];

        await CompactionProvider.CompactAsync(probe, messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.InRange(probe.Estimate, ContextEstimate.ImageTokens, ContextEstimate.ImageTokens + 50);
    }

    private sealed class EstimateProbe() : CompactionStrategy(CompactionTriggers.Always)
    {
        public int Estimate { get; private set; }

        protected override ValueTask<bool> CompactCoreAsync(CompactionMessageIndex index, ILogger logger, CancellationToken cancellationToken)
        {
            Estimate = ContextEstimate.Of(index);
            return ValueTask.FromResult(false);
        }
    }
}
