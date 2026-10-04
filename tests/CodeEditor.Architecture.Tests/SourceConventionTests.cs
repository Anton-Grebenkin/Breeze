using CodeEditor.Architecture.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// Source conventions in src: file size and one public type per file.
/// </summary>
public sealed class SourceConventionTests
{
    private const int MaxLinesPerFile = 300;

    [Fact]
    public void SourceFiles_AreWithinLineLimit()
    {
        var tooLong = RepositoryPaths.SourceFiles()
            .Select(path => (Path: path, Lines: File.ReadLines(path).Count()))
            .Where(file => file.Lines > MaxLinesPerFile)
            .Select(file => $"{RepositoryPaths.Relative(file.Path)} ({file.Lines})")
            .ToArray();

        Assert.True(tooLong.Length == 0,
            $"Файлы длиннее {MaxLinesPerFile} строк — раздели их: {string.Join(", ", tooLong)}.");
    }

    [Fact]
    public void SourceFiles_DeclareOnePublicTypeNamedAfterFile()
    {
        var violations = RepositoryPaths.SourceFiles()
            .Select(FindViolation)
            .OfType<string>()
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    private static string? FindViolation(string path)
    {
        var publicTypes = PublicTopLevelTypeNames(File.ReadAllText(path));
        var relative = RepositoryPaths.Relative(path);

        if (publicTypes.Count > 1)
        {
            return $"{relative}: несколько публичных типов ({string.Join(", ", publicTypes)}).";
        }

        // XAML code-behind is named Name.xaml.cs, so take the name up to the first dot.
        var expectedName = Path.GetFileName(path).Split('.')[0];
        return publicTypes.Count == 1 && publicTypes[0] != expectedName
            ? $"{relative}: публичный тип {publicTypes[0]} должен лежать в файле {publicTypes[0]}.cs."
            : null;
    }

    private static List<string> PublicTopLevelTypeNames(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        return root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>()
            .Where(member => member.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Select(TypeName)
            .OfType<string>()
            .ToList();
    }

    private static string? TypeName(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
        DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
        _ => null,
    };
}
