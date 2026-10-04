using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Files;
using CodeEditor.Shell.Palette;

namespace CodeEditor.Benchmarks;

/// <summary>
/// <c>Ctrl+P</c> file matching over 10,000 files, run on every key press. Budget: ≤ 16 ms (one frame).
/// </summary>
[MemoryDiagnoser]
public class FileMatcherBenchmarks
{
    private const int FileCount = 10_000;
    private const int RecentCount = 50;

    private static readonly string[] Folders = ["src/Core", "src/Shell", "src/Modules/Git", "tests/Core", "docs", "tools/build"];
    private static readonly string[] Stems = ["Program", "Helper", "Service", "ViewModel", "Controller", "Options", "Repository"];
    private static readonly string[] Extensions = [".cs", ".xaml", ".json", ".md"];

    private IndexedFile[] _files = [];
    private Dictionary<string, int> _recent = [];

    [Params("p", "prog", "svcvm", "src/modgit", "zzzz")]
    public string Query { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _files = [.. Enumerable.Range(0, FileCount).Select(CreateFile)];
        _recent = _files.Take(RecentCount).Select((file, rank) => (file.FullPath, rank)).ToDictionary(pair => pair.FullPath, pair => pair.rank);
    }

    [Benchmark]
    public int Match() => FileMatcher.Match(_files, Query, _recent).Length;

    private static IndexedFile CreateFile(int index)
    {
        var name = $"{Stems[index % Stems.Length]}{index}{Extensions[index % Extensions.Length]}";
        var relative = $"{Folders[index % Folders.Length]}/Sub{index % 40}/{name}";
        return new IndexedFile(Path.Combine(@"C:\repo", relative), relative, name);
    }
}
