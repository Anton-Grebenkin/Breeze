namespace CodeEditor.Core.Text;

/// <summary>
/// Line diff with Myers' algorithm: O((N+M)·D), where D is the number of differences. The common prefix and suffix are
/// trimmed first, so an edit in a large file compares only a few lines. Beyond <see cref="MaxDistance"/> differences
/// the result is "everything removed, everything added" to bound memory.
/// </summary>
public static class LineDiff
{
    public const int MaxDistance = 4000;
    public const int DefaultContext = 3;

    public static IReadOnlyList<DiffLine> Compute(string oldText, string newText)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var a = SplitLines(oldText);
        var b = SplitLines(newText);
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        int endA = a.Length, endB = b.Length;
        while (endA > prefix && endB > prefix && a[endA - 1] == b[endB - 1])
        {
            endA--;
            endB--;
        }

        var result = new List<DiffLine>(a.Length + b.Length);
        for (var i = 0; i < prefix; i++)
        {
            result.Add(new DiffLine(DiffKind.Unchanged, a[i], i + 1, i + 1));
        }

        result.AddRange(Middle(a, b, prefix, endA, endB));
        for (int i = endA, j = endB; i < a.Length; i++, j++)
        {
            result.Add(new DiffLine(DiffKind.Unchanged, a[i], i + 1, j + 1));
        }

        return result;
    }

    /// <summary>Changes with <paramref name="context"/> lines around them; adjacent hunks merge.</summary>
    public static IReadOnlyList<DiffHunk> Hunks(IReadOnlyList<DiffLine> lines, int context = DefaultContext)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var hunks = new List<DiffHunk>();
        var index = 0;
        while (index < lines.Count)
        {
            if (lines[index].Kind == DiffKind.Unchanged)
            {
                index++;
                continue;
            }

            var start = Math.Max(0, index - context);
            var end = index;
            var lastChange = index;
            while (end < lines.Count && end - lastChange <= context * 2)
            {
                if (lines[end].Kind != DiffKind.Unchanged)
                {
                    lastChange = end;
                }

                end++;
            }

            end = Math.Min(lines.Count, lastChange + context + 1);
            var slice = lines.Skip(start).Take(end - start).ToList();
            hunks.Add(new DiffHunk(FirstLine(slice, line => line.OldLine), FirstLine(slice, line => line.NewLine), slice));
            index = end;
        }

        return hunks;
    }

    private static string[] SplitLines(string text) =>
        text.Length == 0 ? [] : [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];

    private static int FirstLine(List<DiffLine> slice, Func<DiffLine, int> number) =>
        slice.Select(number).FirstOrDefault(line => line > 0);

    /// <summary>Myers over [prefix, endA) × [prefix, endB), backtracking through the saved frontiers.</summary>
    private static List<DiffLine> Middle(string[] a, string[] b, int prefix, int endA, int endB)
    {
        int n = endA - prefix, m = endB - prefix;
        if (n == 0 && m == 0)
        {
            return [];
        }

        var trace = Forward(a, b, prefix, n, m);
        return trace is null ? ReplaceAll(a, b, prefix, endA, endB) : Backtrack(a, b, prefix, n, m, trace);
    }

    // The frontier of step d is stored as the slice [-(d+1), d+1]: O(D²) memory instead of O(D·(N+M)).
    private static List<int[]>? Forward(string[] a, string[] b, int prefix, int n, int m)
    {
        var max = n + m;
        var offset = max + 1;
        var v = new int[2 * max + 3];
        var trace = new List<int[]>();
        for (var d = 0; d <= Math.Min(max, MaxDistance); d++)
        {
            trace.Add(v[(offset - d - 1)..(offset + d + 2)]);
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[prefix + x] == b[prefix + y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x >= n && y >= m)
                {
                    return trace;
                }
            }
        }

        return null;
    }

    private static List<DiffLine> Backtrack(string[] a, string[] b, int prefix, int n, int m, List<int[]> trace)
    {
        var reversed = new List<DiffLine>();
        int x = n, y = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var front = trace[d];
            int At(int k) => front[k + d + 1];
            var k = x - y;
            var previousK = k == -d || (k != d && At(k - 1) < At(k + 1)) ? k + 1 : k - 1;
            var previousX = d == 0 ? 0 : At(previousK);
            var previousY = previousX - previousK;
            while (x > previousX && y > previousY)
            {
                x--;
                y--;
                reversed.Add(new DiffLine(DiffKind.Unchanged, a[prefix + x], prefix + x + 1, prefix + y + 1));
            }

            if (d > 0)
            {
                reversed.Add(x == previousX
                    ? new DiffLine(DiffKind.Added, b[prefix + y - 1], 0, prefix + y)
                    : new DiffLine(DiffKind.Removed, a[prefix + x - 1], prefix + x, 0));
            }

            x = previousX;
            y = previousY;
        }

        reversed.Reverse();
        return reversed;
    }

    private static List<DiffLine> ReplaceAll(string[] a, string[] b, int prefix, int endA, int endB)
    {
        var lines = new List<DiffLine>((endA - prefix) + (endB - prefix));
        for (var i = prefix; i < endA; i++)
        {
            lines.Add(new DiffLine(DiffKind.Removed, a[i], i + 1, 0));
        }

        for (var j = prefix; j < endB; j++)
        {
            lines.Add(new DiffLine(DiffKind.Added, b[j], 0, j + 1));
        }

        return lines;
    }
}
