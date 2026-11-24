using System.Windows.Media;
using BenchmarkDotNet.Attributes;
using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Benchmarks;

/// <summary>
/// The part of input latency the editor's own code decides (budget: 16 ms per character): one edit in the middle of a
/// 10,000-line file and highlighting the screen around it again. Layout and drawing are WPF's and are not measured
/// here.
/// </summary>
[MemoryDiagnoser]
public class TypingBenchmarks
{
    private const int Lines = 10_000;
    private const int ScreenLines = 60;

    private static readonly Dictionary<string, string> Samples = new(StringComparer.Ordinal)
    {
        ["Program.cs"] = """
            using System;
            namespace Demo;
            /// <summary>Entry point.</summary>
            public sealed class Program
            {
                public static int Main(string[] args)
                {
                    var name = args.Length > 0 ? args[0] : "World"; // greeting
                    Console.WriteLine($"Hello, {name}! {42:N2}");
                    return 0;
                }
            }
            """,
        ["data.json"] = """
            {
              "name": "breeze",
              "version": "0.1.0",
              "items": [1, 2.5, true, null, "text with \"quotes\""],
              "nested": { "path": "src/App.cs", "enabled": false }
            }
            """,
        ["notes.md"] = """
            # Heading
            Some *emphasis*, **strong** text and `inline code` with a [link](https://example.com).
            - item one
            - item two
            ```
            code block line
            ```
            """,
    };

    private TextDocument _document = null!;
    private DocumentHighlighter _highlighter = null!;
    private int _offset;
    private int _firstScreenLine;
    private bool _typed;

    [ParamsSource(nameof(FileNames))]
    public string FileName { get; set; } = string.Empty;

    public static IEnumerable<string> FileNames => Samples.Keys;

    [GlobalSetup]
    public void Setup()
    {
        var palette = SyntaxColorClassifier.PaletteKeys.Distinct(StringComparer.Ordinal).ToDictionary(static key => key, static _ => Colors.Gray, StringComparer.Ordinal);
        var definition = HighlightingDefinitions.CreateManager(palette).GetDefinition(LanguageCatalog.ForFile(FileName)!.Highlighting);
        var source = Samples[FileName].Split('\n');
        _document = new TextDocument(string.Join('\n', Enumerable.Range(0, Lines).Select(index => source[index % source.Length])));
        _highlighter = new DocumentHighlighter(_document, definition);
        for (var line = 1; line <= _document.LineCount; line++)
        {
            _highlighter.HighlightLine(line);
        }

        var middle = _document.GetLineByNumber(_document.LineCount / 2);
        _offset = middle.Offset;
        _firstScreenLine = middle.LineNumber - (ScreenLines / 2);
    }

    [GlobalCleanup]
    public void Cleanup() => _highlighter.Dispose();

    /// <summary>One keystroke: a character typed or deleted in turn, so the document stays the same size.</summary>
    [Benchmark]
    public int EditAndHighlightScreen()
    {
        if (_typed)
        {
            _document.Remove(_offset, 1);
        }
        else
        {
            _document.Insert(_offset, "x");
        }

        _typed = !_typed;
        var sections = 0;
        for (var line = _firstScreenLine; line < _firstScreenLine + ScreenLines; line++)
        {
            sections += _highlighter.HighlightLine(line).Sections.Count;
        }

        return sections;
    }
}
