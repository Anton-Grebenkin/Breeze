using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CodeEditor.Modules.Agent.Services.Context;

/// <summary>
/// History token estimate for compaction thresholds: the framework's estimate (bytes/4), except that an image counts
/// as <see cref="ImageTokens"/> instead of its size. The framework counts a 400 KB screenshot as 100K tokens while the
/// model counts about 1.5K; without the correction an attached image would trigger compaction at once.
/// </summary>
internal static class ContextEstimate
{
    /// <summary>Model tokens per image (~1000×1000 px for Claude and GPT).</summary>
    public const int ImageTokens = 1_600;

    /// <summary>O(history messages) per threshold check, without allocations.</summary>
    public static int Of(CompactionMessageIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var images = 0;
        long imageBytes = 0;
        foreach (var group in index.Groups)
        {
            if (group.IsExcluded)
            {
                continue;
            }

            foreach (var message in group.Messages)
            {
                foreach (var content in message.Contents)
                {
                    if (content is DataContent image)
                    {
                        images++;
                        imageBytes += (long)image.Data.Length + (image.MediaType?.Length ?? 0) + (image.Name?.Length ?? 0);
                    }
                }
            }
        }

        return images == 0
            ? index.IncludedTokenCount
            : (int)Math.Max(0, index.IncludedTokenCount - (imageBytes / 4) + ((long)images * ImageTokens));
    }
}
