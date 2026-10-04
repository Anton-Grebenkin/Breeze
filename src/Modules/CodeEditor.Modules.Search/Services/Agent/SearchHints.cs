using CodeEditor.Modules.Search.Resources;

namespace CodeEditor.Modules.Search.Services.Agent;

/// <summary>
/// Hints appended to <c>search_text</c> results when the model searches in circles (seen in practice: 11 searches
/// in a row over one file, a third of all searches empty). After several empty results in a row it suggests
/// following the call chain from an entry point or delegating to the explorer; after several searches over the same
/// file it suggests reading the file. Counters are per chat and reset by a successful search.
/// </summary>
public sealed class SearchHints
{
    public const int EmptyStreak = 5;
    public const int SameFileStreak = 3;

    private readonly Lock _gate = new();
    private int _empty;
    private string? _lastFile;
    private int _sameFile;

    /// <returns>A hint to append to the result, or an empty string.</returns>
    public string After(string? include, bool found)
    {
        lock (_gate)
        {
            _empty = found ? 0 : _empty + 1;
            var file = SingleFile(include);
            _sameFile = file is not null && file == _lastFile ? _sameFile + 1 : 1;
            _lastFile = file;

            if (_empty == EmptyStreak)
            {
                return "\n" + string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ToolEmptyStreakHint, EmptyStreak);
            }

            return file is not null && _sameFile == SameFileStreak ? "\n" + string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ToolSameFileHint, file) : string.Empty;
        }
    }

    // A glob whose file name has no wildcards targets a single file.
    private static string? SingleFile(string? include)
    {
        if (string.IsNullOrWhiteSpace(include) || include.Contains(',', StringComparison.Ordinal))
        {
            return null;
        }

        var name = include.Trim();
        var fileName = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        return fileName.Length > 0 && fileName.Contains('.', StringComparison.Ordinal) && fileName.IndexOfAny(['*', '?', '{']) < 0 ? name : null;
    }
}
