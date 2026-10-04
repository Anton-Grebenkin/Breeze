using System.Text;

namespace CodeEditor.Modules.Agent.Services.Prompts;

/// <summary>
/// Splits reasoning aloud out of the model text stream: <c>&lt;thinking&gt;…&lt;/thinking&gt;</c> (what the prompt asks
/// of models without native reasoning) and <c>&lt;think&gt;…&lt;/think&gt;</c> (used by some open models). A tag may be
/// split across chunks, so a tail that looks like a tag start is held until the next chunk. O(chunk length).
/// </summary>
public sealed class ThinkingTagSplitter
{
    private static readonly string[] OpenTags = ["<thinking>", "<think>"];
    private static readonly string[] CloseTags = ["</thinking>", "</think>"];

    private readonly StringBuilder _pending = new();
    private bool _inside;
    private bool _trimNext;

    /// <summary>Parses the next chunk; returns text and reasoning parts in order.</summary>
    public IReadOnlyList<TaggedText> Push(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _pending.Append(chunk);
        var parts = new List<TaggedText>();
        while (_pending.Length > 0)
        {
            var text = _pending.ToString();
            var (index, tag) = Find(text, _inside ? CloseTags : OpenTags);
            if (index < 0)
            {
                var keep = PartialTagLength(text, _inside ? CloseTags : OpenTags);
                Emit(parts, text[..^keep]);
                _pending.Remove(0, text.Length - keep);
                break;
            }

            Emit(parts, text[..index]);
            _pending.Remove(0, index + tag.Length);
            _inside = !_inside;
            _trimNext = true;
            if (!_inside)
            {
                parts.Add(new TaggedText(TaggedTextKind.ThinkingDone, string.Empty));
            }
        }

        return parts;
    }

    /// <summary>The response ended: the held tail is emitted as is and unclosed reasoning is finished.</summary>
    public IReadOnlyList<TaggedText> Flush()
    {
        var parts = new List<TaggedText>();
        Emit(parts, _pending.ToString());
        _pending.Clear();
        if (_inside)
        {
            parts.Add(new TaggedText(TaggedTextKind.ThinkingDone, string.Empty));
        }

        (_inside, _trimNext) = (false, false);
        return parts;
    }

    // Whitespace right after a tag is model formatting, not text.
    private void Emit(List<TaggedText> parts, string text)
    {
        if (_trimNext)
        {
            text = text.TrimStart();
            _trimNext = text.Length == 0;
        }

        if (text.Length > 0)
        {
            parts.Add(new TaggedText(_inside ? TaggedTextKind.Thinking : TaggedTextKind.Text, text));
        }
    }

    private static (int Index, string Tag) Find(string text, string[] tags)
    {
        var (best, found) = (-1, string.Empty);
        foreach (var tag in tags)
        {
            var index = text.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (best < 0 || index < best))
            {
                (best, found) = (index, tag);
            }
        }

        return (best, found);
    }

    // How many trailing characters may start a tag ("<thi"); they are held until the next chunk.
    private static int PartialTagLength(string text, string[] tags)
    {
        var longest = 0;
        foreach (var tag in tags)
        {
            for (var length = Math.Min(tag.Length - 1, text.Length); length > longest; length--)
            {
                if (text.EndsWith(tag[..length], StringComparison.OrdinalIgnoreCase))
                {
                    longest = length;
                    break;
                }
            }
        }

        return longest;
    }
}
