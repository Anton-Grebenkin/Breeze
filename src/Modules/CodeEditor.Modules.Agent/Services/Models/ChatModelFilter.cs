using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Keeps only chat models from a service list: no image, video, speech or embedding models, and no dated snapshots
/// when an undated model exists (<c>gpt-5-mini-2025-08-07</c> next to <c>gpt-5-mini</c>). ProxyAPI lists over 600
/// models, about half of them usable by the agent. O(n) over the list.
/// </summary>
public static partial class ChatModelFilter
{
    // Vendors and name markers unrelated to chat: image and video generation, speech, embeddings, moderation.
    private static readonly FrozenSet<string> MediaVendors = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "recraft", "black-forest-labs", "krea", "kwaivgi", "voyageai", "sentence-transformers", "intfloat", "baai",
        "thenlper", "sourceful");

    private static readonly string[] MediaMarkers =
    [
        "image", "video", "tts", "audio", "speech", "embed", "realtime", "transcribe", "dall-e", "sora", "whisper",
        "veo-", "imagen", "lyria", "moderation", "hailuo", "happyhorse", "wan-", "rerank", "search-preview",
    ];

    public static IReadOnlyList<string> Chat(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var chat = ids.Where(IsChat).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var undated = chat.Where(id => !Snapshot().IsMatch(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. chat.Where(id => !Snapshot().IsMatch(id) || !undated.Contains(Snapshot().Replace(id, string.Empty)))];
    }

    private static bool IsChat(string id)
    {
        var slash = id.IndexOf('/', StringComparison.Ordinal);
        return slash > 0
            && !MediaVendors.Contains(id[..slash])
            && !MediaMarkers.Any(marker => id.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    // A date at the end of the id: "-20260223", "-2025-08-07", "-0731", "-2507" (month and year, or month and day).
    [GeneratedRegex(@"-(\d{8}|\d{4}-\d{2}-\d{2}|\d{4}|\d{2}-\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Snapshot();
}
