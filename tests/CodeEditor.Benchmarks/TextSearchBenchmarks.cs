using BenchmarkDotNet.Attributes;
using CodeEditor.Core.Context;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Search.Services;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeEditor.Benchmarks;

/// <summary>
/// Search over 10,000 files on disk (≈ 2 KB each, ≈ 20 MB): folder indexing and a full search pass.
/// Files are in the OS cache after the first run, as with repeated searches in the editor.
/// </summary>
[MemoryDiagnoser]
public class TextSearchBenchmarks
{
    private const int FileCount = 10_000;

    private static readonly Dictionary<string, string> NoUnsaved = [];

    private string _root = string.Empty;
    private Workspace _workspace = null!;
    private FileIndex _index = null!;
    private TextSearchService _search = null!;

    [Params("Helper", "редкое_слово", @"void \w+\(")]
    public string Pattern { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "CodeEditor.Benchmarks", "search-10k");
        if (!Directory.Exists(_root))
        {
            GenerateFiles(_root);
        }

        var fileSystem = new PhysicalFileSystem();
        _workspace = new Workspace(fileSystem, new ContextKeyService(), NullLogger<Workspace>.Instance);
        _index = new FileIndex(_workspace, fileSystem, NullLogger<FileIndex>.Instance);
        _workspace.Open(_root);
        _index.WhenReady.GetAwaiter().GetResult();
        _search = new TextSearchService(_index, fileSystem, new TestOptionsMonitor<SearchSettings>(new SearchSettings()), NullLogger<TextSearchService>.Instance);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _index.Dispose();
        _workspace.Dispose();
    }

    [Benchmark]
    public async Task<int> Search()
    {
        var options = new TextSearchOptions(Pattern) { UseRegex = Pattern.Contains('\\', StringComparison.Ordinal) };
        var count = 0;
        await foreach (var result in _search.SearchAsync(options, NoUnsaved, CancellationToken.None))
        {
            count += result.Matches.Count;
        }

        return count;
    }

    [Benchmark]
    public int Index()
    {
        _workspace.Open(_root);
        _index.WhenReady.GetAwaiter().GetResult();
        return _index.Files.Count;
    }

    private static void GenerateFiles(string root)
    {
        for (var i = 0; i < FileCount; i++)
        {
            var folder = Directory.CreateDirectory(Path.Combine(root, $"module{i % 20}", $"part{i % 50}")).FullName;
            var lines = Enumerable.Range(0, 60).Select(line => line % 15 == 0
                ? $"    public void Method{line}() => Helper.Run({i});"
                : $"    // строка {line} файла {i}: обычный комментарий без совпадений");
            var text = $"namespace Sample{i};\r\n\r\npublic sealed class Class{i}\r\n{{\r\n{string.Join("\r\n", lines)}\r\n}}\r\n";
            if (i == FileCount / 2)
            {
                text += "// редкое_слово\r\n";
            }

            File.WriteAllText(Path.Combine(folder, $"Class{i}.cs"), text);
        }
    }
}
