using System.Globalization;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.TextEditor.Resources;

namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Locates the <c>apply_patch</c> hunks of one file the way Codex does: top to bottom, each below the previous one, so
/// N identical hunks take N occurrences in a row. The first match wins where something pins the place: an "@@" anchor,
/// the previous hunk, or the same lines coming again in a later hunk. A hunk nothing pins must be unique, and a hunk
/// written out of order is still found: the search falls back to the whole file.
/// </summary>
public sealed class HunkLocator(string text, string relative)
{
    private int _cursor;

    /// <param name="anchors">"@@" lines: the hunk is searched below them, in order.</param>
    /// <param name="repeatsLater">A later hunk of the file has the same lines: a series over successive occurrences.</param>
    public LocatedFragment Locate(string oldText, string newText, IReadOnlyList<string> anchors, bool repeatsLater)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        var start = BelowAnchors(anchors);
        var pinned = anchors.Count > 0 || _cursor > 0 || repeatsLater;
        var fragment = (pinned ? FragmentLocator.LocateBelow(text, oldText, newText, relative, start) : null)
            ?? FragmentLocator.Locate(text, oldText, newText, relative);
        _cursor = fragment.Offset + fragment.Length;
        return fragment;
    }

    // An anchor above the previous hunk ("@@ class Order" repeated for the next method) still counts, but the hunk
    // itself is searched no higher than the previous one.
    private int BelowAnchors(IReadOnlyList<string> anchors)
    {
        var lines = anchors.Select(anchor => anchor.Trim()).Where(anchor => anchor.Length > 0).ToList();
        return After(lines, _cursor, out _) ?? Math.Max(_cursor, After(lines, 0, out var missing) ?? throw NotFound(missing));
    }

    /// <summary>The line after the last anchor; anchors match in order, each below the previous.</summary>
    /// <returns><c>null</c> if an anchor (<paramref name="missing"/>) is not found.</returns>
    private int? After(List<string> anchors, int position, out string missing)
    {
        missing = string.Empty;
        foreach (var anchor in anchors)
        {
            var found = text.IndexOf(anchor, position, StringComparison.Ordinal);
            if (found < 0)
            {
                missing = anchor;
                return null;
            }

            position = text.IndexOf('\n', found) is var end and >= 0 ? end + 1 : text.Length;
        }

        return position;
    }

    private AgentToolException NotFound(string anchor) =>
        new(string.Format(CultureInfo.CurrentCulture, Strings.AnchorNotFound, relative, anchor));
}
