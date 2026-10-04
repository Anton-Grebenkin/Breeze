using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Text;

namespace CodeEditor.Benchmarks;

/// <summary>
/// Quick open filter (Ctrl+P): one pattern over 10,000 paths.
/// </summary>
[MemoryDiagnoser]
public class FuzzyMatcherBenchmarks
{
    private const int PathCount = 10_000;

    private string[] _paths = [];

    [Params("mwvm", "srcplatcore", "zzz")]
    public string Pattern { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup() => _paths = SamplePaths.Generate(PathCount);

    [Benchmark]
    public int MatchAllPaths()
    {
        var matches = 0;
        foreach (var path in _paths)
        {
            if (FuzzyMatcher.TryMatch(Pattern, path, out _))
            {
                matches++;
            }
        }

        return matches;
    }
}
