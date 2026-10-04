using System.Xml.Linq;
using CodeEditor.Architecture.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// Localization (ADR 0011): UI texts and messages live only in <c>Resources/Strings.resx</c> (English) and
/// <c>Strings.ru.resx</c> (Russian). A Cyrillic string in code or markup is a missed translation. Comments and
/// XML docs are in English too.
/// </summary>
public sealed class LocalizationTests
{
    private const string NeutralResources = "Strings.resx";
    private const string RussianResources = "Strings.ru.resx";
    private const int MaxReported = 2000;

    private static readonly SyntaxKind[] CommentTrivia =
    [
        SyntaxKind.SingleLineCommentTrivia,
        SyntaxKind.MultiLineCommentTrivia,
        SyntaxKind.SingleLineDocumentationCommentTrivia,
        SyntaxKind.MultiLineDocumentationCommentTrivia,
    ];

    private static readonly SyntaxKind[] StringTokens =
    [
        SyntaxKind.StringLiteralToken,
        SyntaxKind.InterpolatedStringTextToken,
        SyntaxKind.SingleLineRawStringLiteralToken,
        SyntaxKind.MultiLineRawStringLiteralToken,
        SyntaxKind.CharacterLiteralToken,
    ];

    [Fact]
    public void Code_HasNoCyrillicStrings()
    {
        var violations = RepositoryPaths.SourceFiles()
            .SelectMany(path => CyrillicStrings(path).Select(line => $"{RepositoryPaths.Relative(path)}:{line}"))
            .ToArray();

        Assert.True(violations.Length == 0, Report("Строки с кириллицей в коде — перенеси их в Resources/Strings.resx", violations));
    }

    [Fact]
    public void Xaml_HasNoCyrillicText()
    {
        var violations = RepositoryPaths.SourceFiles("*.xaml")
            .SelectMany(path => CyrillicXaml(path).Select(text => $"{RepositoryPaths.Relative(path)}: {text}"))
            .ToArray();

        Assert.True(violations.Length == 0, Report("Текст с кириллицей в разметке — возьми его из ресурсов через {x:Static}", violations));
    }

    [Fact]
    public void Comments_AreInEnglish()
    {
        var code = RepositoryPaths.SourceFiles()
            .SelectMany(path => CyrillicComments(path).Select(line => $"{RepositoryPaths.Relative(path)}:{line}"));
        var markup = RepositoryPaths.SourceFiles("*.xaml")
            .SelectMany(path => XDocument.Load(path).DescendantNodes().OfType<XComment>()
                .Where(comment => HasCyrillic(comment.Value))
                .Select(comment => $"{RepositoryPaths.Relative(path)}: {comment.Value.Trim()}"));
        var violations = code.Concat(markup).ToArray();

        Assert.True(violations.Length == 0, Report("Комментарии с кириллицей — комментарии и XML-doc пишутся по-английски", violations));
    }

    [Fact]
    public void Resources_HaveRussianTranslationForEveryString()
    {
        var problems = RepositoryPaths.SourceFiles(NeutralResources).SelectMany(CompareTranslations).ToArray();

        Assert.True(problems.Length == 0, Report("Ресурсы без пары", problems));
    }

    private static IEnumerable<int> CyrillicStrings(string path) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
            .DescendantTokens()
            .Where(token => StringTokens.Contains(token.Kind()) && HasCyrillic(token.Text))
            .Select(token => token.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            .Distinct();

    private static IEnumerable<int> CyrillicComments(string path) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot()
            .DescendantTrivia()
            .Where(trivia => CommentTrivia.Contains(trivia.Kind()) && HasCyrillic(trivia.ToFullString()))
            .Select(trivia => trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1)
            .Distinct();

    private static IEnumerable<string> CyrillicXaml(string path)
    {
        var document = XDocument.Load(path);
        var attributes = document.Descendants().SelectMany(element => element.Attributes()).Select(attribute => attribute.Value);
        var texts = document.DescendantNodes().OfType<XText>().Select(text => text.Value);
        return attributes.Concat(texts).Where(HasCyrillic);
    }

    private static IEnumerable<string> CompareTranslations(string neutral)
    {
        var relative = RepositoryPaths.Relative(neutral);
        var russian = Path.Combine(Path.GetDirectoryName(neutral)!, RussianResources);
        if (!File.Exists(russian))
        {
            return [$"{relative}: нет {RussianResources}"];
        }

        var english = Keys(neutral);
        var translated = Keys(russian);
        return english.Except(translated).Select(key => $"{relative}: «{key}» без русского перевода")
            .Concat(translated.Except(english).Select(key => $"{relative}: «{key}» есть только в {RussianResources}"))
            .Concat(EmptyValues(neutral).Concat(EmptyValues(russian)).Select(key => $"{relative}: «{key}» пуст"));
    }

    private static HashSet<string> Keys(string path) =>
        [.. XDocument.Load(path).Root!.Elements("data").Select(data => (string)data.Attribute("name")!)];

    private static IEnumerable<string> EmptyValues(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(data => string.IsNullOrWhiteSpace((string?)data.Element("value")))
            .Select(data => (string)data.Attribute("name")!);

    private static bool HasCyrillic(string text) => text.AsSpan().ContainsAnyInRange('Ѐ', 'ӿ');

    private static string Report(string title, string[] items) =>
        $"{title} ({items.Length}):{Environment.NewLine}{string.Join(Environment.NewLine, items.Take(MaxReported))}";
}
