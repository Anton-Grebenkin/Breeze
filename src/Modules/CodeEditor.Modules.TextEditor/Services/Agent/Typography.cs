namespace CodeEditor.Modules.TextEditor.Services.Agent;

/// <summary>
/// Characters models and files often write differently: dashes and minus, curly and angle quotes, non-breaking and
/// thin spaces. Compared in plain ASCII form, like Codex's apply_patch <c>seek_sequence</c>. Each character maps to
/// exactly one, so line length is preserved.
/// </summary>
public static class Typography
{
    public static char Plain(char character) => character switch
    {
        '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
        '‘' or '’' or '‚' or '‛' => '\'',
        '“' or '”' or '„' or '‟' or '«' or '»' => '"',
        ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or ' ' or '　' => ' ',
        _ => character,
    };

    /// <summary>Strings are equal if their plain forms are. O(n), allocation-free.</summary>
    public static bool PlainEquals(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (Plain(left[index]) != Plain(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
