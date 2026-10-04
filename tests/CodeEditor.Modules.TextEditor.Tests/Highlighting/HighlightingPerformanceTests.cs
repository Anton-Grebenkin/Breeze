using System.Diagnostics;
using System.Globalization;
using System.Text;
using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Modules.TextEditor.Wpf.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeEditor.Modules.TextEditor.Tests.Highlighting;

/// <summary>
/// Highlighting is a hot path (input latency ≤ 16 ms, ADR 0036): definition regexes must not backtrack
/// catastrophically on long uniform lines, and a 10,000-line file must highlight fast.
/// </summary>
public sealed class HighlightingPerformanceTests
{
    private const int PathologicalLength = 3_000;
    private const int MeasuredLines = 10_000;
    private const int ScreenLines = 60;

    // Long lines of one or two repeated chars: greedy nested repetitions go exponential on them, and patterns that
    // scan to the line end from every position go quadratic.
    private static readonly string[] PathologicalUnits =
    [
        "a", " ", "\t", "0", "_", "-", ".", ":", "=", "#", "$", "%", "@", "!", "*", "/", "\\", "\"", "'", "`", "<", ">", "{", "}",
        "[", "]", "(", ")", "|", ",", ";", "&", "?", "~", "a ", "a.", "a-", "a:", "a=", "0.", "${", "{{", "<a ", "a(", "\"a", "--",
        "//", "/*", "#{", "- ", "a: ", "[[", "%%", "\\\"", "a\t", "$(", "<<",
    ];

    // The built-in C# definition, as a baseline for the custom ones.
    private static readonly HighlightingSample CSharpSample = new("Program.cs", """
        // Program
        using System;
        namespace Demo;
        public sealed class Program
        {
            /// <summary>Entry point.</summary>
            public static int Main(string[] args)
            {
                var name = args.Length > 0 ? args[0] : "World";
                Console.WriteLine($"Hello, {name}! {42:N2}");
                return 0;
            }
        }
        """);

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PathologicalBudget = TimeSpan.FromSeconds(3);

    public static TheoryData<string> Names => [.. HighlightingDefinitions.Names.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Names))]
    public void Definition_HasNoCatastrophicBacktracking(string name)
    {
        var definition = TestHighlighting.CreateManager().GetDefinition(name);
        RegexTimeouts.Apply(definition, MatchTimeout);
        var text = string.Join('\n', PathologicalUnits.Select(unit => string.Concat(Enumerable.Repeat(unit, PathologicalLength / unit.Length))));

        var stopwatch = Stopwatch.StartNew();
        TestHighlighting.Highlight(definition, text);

        Assert.True(stopwatch.Elapsed < PathologicalBudget, $"{name}: {stopwatch.ElapsedMilliseconds} мс на {PathologicalUnits.Length} длинных строк.");
    }

    // Measurement for ADR 0036: run explicitly (--explicit only); timings depend on the machine.
    [Fact(Explicit = true)]
    public void Measure_TenThousandLines()
    {
        var report = new StringBuilder();
        foreach (var (id, sample) in HighlightingSamples.All().Prepend(("csharp", CSharpSample)))
        {
            report.AppendLine(Measure(id, sample));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
    }

    private static string Measure(string id, HighlightingSample sample)
    {
        var stopwatch = Stopwatch.StartNew();
        var definition = TestHighlighting.CreateManager().GetDefinition(LanguageCatalog.ForFile(sample.FileName)!.Highlighting);
        _ = definition.MainRuleSet;
        var load = stopwatch.Elapsed;

        // Warm-up: the first pass parses the regexes and JIT-compiles the highlighter.
        TestHighlighting.Highlight(definition, sample.Text);

        var document = new TextDocument(Repeat(sample.Text, MeasuredLines));
        using var highlighter = new DocumentHighlighter(document, definition);
        stopwatch.Restart();
        for (var line = 1; line <= document.LineCount; line++)
        {
            highlighter.HighlightLine(line);
        }

        var full = stopwatch.Elapsed;

        // Typing a char mid-file, then redrawing the screen around the edited line.
        var middle = document.GetLineByNumber(document.LineCount / 2);
        document.Insert(middle.Offset, "x");
        stopwatch.Restart();
        for (var line = middle.LineNumber - (ScreenLines / 2); line < middle.LineNumber + (ScreenLines / 2); line++)
        {
            highlighter.HighlightLine(line);
        }

        var screen = stopwatch.Elapsed;
        return string.Create(CultureInfo.InvariantCulture,
            $"{id,-16} load {load.TotalMilliseconds,6:F1} ms | {document.LineCount} lines {full.TotalMilliseconds,7:F1} ms ({full.TotalMicroseconds / document.LineCount,5:F1} us/line) | screen after edit {screen.TotalMilliseconds,5:F2} ms");
    }

    private static string Repeat(string text, int lines)
    {
        var source = text.Split('\n');
        return string.Join('\n', Enumerable.Range(0, lines).Select(index => source[index % source.Length]));
    }
}
