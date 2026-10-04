using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Replaces text inside a Word paragraph split into runs with different formatting. Matches are found in the joined
/// paragraph text and replaced in its pieces: the new text goes into the run where the match starts and takes its
/// formatting; the other pieces of the match are removed. Paragraph properties and nearby text stay unchanged. Text
/// boxes inside the paragraph have their own paragraphs; tracked deletions and moves are ignored.
/// </summary>
internal sealed class ParagraphText
{
    private readonly List<Text> _parts;
    private readonly List<int> _starts = [];
    private readonly string _text;

    public ParagraphText(Paragraph paragraph)
    {
        _parts = [.. paragraph.Descendants<Text>().Where(text => IsOwnVisible(text, paragraph))];
        var offset = 0;
        foreach (var part in _parts)
        {
            _starts.Add(offset);
            offset += part.Text.Length;
        }

        _text = string.Concat(_parts.Select(part => part.Text));
    }

    public string Text => _text;

    public int Count(string find) => Occurrences(find).Count;

    /// <returns>The number of replaced occurrences.</returns>
    public int Replace(string find, string replacement)
    {
        var occurrences = Occurrences(find);

        // From the end: replacing a later occurrence does not shift the pieces of earlier ones.
        for (var index = occurrences.Count - 1; index >= 0; index--)
        {
            ReplaceAt(occurrences[index], find.Length, replacement);
        }

        return occurrences.Count;
    }

    // The paragraph's own text: not in a nested text box paragraph and not a tracked deletion or move, since such text
    // is neither shown by Word nor seen by the model.
    private static bool IsOwnVisible(Text text, Paragraph paragraph)
    {
        for (var parent = text.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, paragraph))
            {
                return true;
            }

            if (parent is Paragraph or DeletedRun or MoveFromRun)
            {
                return false;
            }
        }

        return false;
    }

    private List<int> Occurrences(string find)
    {
        var result = new List<int>();
        for (var index = _text.IndexOf(find, StringComparison.Ordinal); index >= 0; index = _text.IndexOf(find, index + find.Length, StringComparison.Ordinal))
        {
            result.Add(index);
        }

        return result;
    }

    private void ReplaceAt(int start, int length, string replacement)
    {
        var (first, firstOffset) = Locate(start);
        var (last, lastOffset) = Locate(start + length - 1);
        var firstText = _parts[first].Text;
        var tail = _parts[last].Text[(lastOffset + 1)..];
        if (first == last)
        {
            Set(first, firstText[..firstOffset] + replacement + tail);
            return;
        }

        Set(first, firstText[..firstOffset] + replacement);
        for (var middle = first + 1; middle < last; middle++)
        {
            Set(middle, string.Empty);
        }

        Set(last, tail);
    }

    private (int Part, int Offset) Locate(int position)
    {
        var part = _starts.BinarySearch(position);
        part = part >= 0 ? part : ~part - 1;

        // Skip empty pieces at the same position: the character is in a non-empty one.
        while (part < _parts.Count - 1 && position - _starts[part] >= _parts[part].Text.Length)
        {
            part++;
        }

        return (part, position - _starts[part]);
    }

    // A line feed in the replacement becomes a line break in the same run.
    private void Set(int index, string text)
    {
        var part = _parts[index];
        var lines = text.Split('\n');
        part.Text = lines[0];
        part.Space = SpaceProcessingModeValues.Preserve;
        OpenXmlElement previous = part;
        foreach (var line in lines.Skip(1))
        {
            var lineBreak = new Break();
            previous.InsertAfterSelf(lineBreak);
            previous = lineBreak.InsertAfterSelf(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
        }
    }
}
