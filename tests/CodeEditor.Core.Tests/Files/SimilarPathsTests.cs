using CodeEditor.Core.Files;

namespace CodeEditor.Core.Tests.Files;

/// <summary>Similar paths for a glob without matches, e.g. <c>**/Catalog/**</c> for the folder <c>src/Acme.Catalog</c>.</summary>
public sealed class SimilarPathsTests
{
    private static readonly string[] Files =
    [
        "src/Acme.Catalog/Migrations/CatalogDbContextModelSnapshot.cs",
        "src/Acme.Catalog/Domain/Products/Product.cs",
        "src/Acme.Core/DataFlow/SoftDeletables/SoftDeleteService.cs",
        "tests/Acme.Catalog.Tests/ProductTests.cs",
        "README.md",
    ];

    [Fact]
    public void FolderPart_SuggestsFoldersContainingIt() =>
        Assert.Equal(["src/Acme.Catalog", "tests/Acme.Catalog.Tests"], SimilarPaths.Suggest(Files, "**/Catalog/**/*.cs"));

    [Fact]
    public void FileName_SuggestsTheFileFirst() =>
        Assert.Equal(
            ["src/Acme.Catalog/Migrations/CatalogDbContextModelSnapshot.cs", "src/Acme.Catalog", "tests/Acme.Catalog.Tests", "src/Acme.Catalog/Migrations"],
            SimilarPaths.Suggest(Files, "**/Catalog/Migrations/CatalogDbContextModelSnapshot.cs"));

    [Fact]
    public void ExactFolderName_ComesFirst() =>
        Assert.Equal("src/Acme.Core/DataFlow/SoftDeletables", SimilarPaths.Suggest(Files, "SoftDeletables")[0]);

    [Theory]
    [InlineData("**/*.cs")]
    [InlineData("")]
    [InlineData("Nothing/Like/This")]
    public void NoLiteralsOrNoMatches_IsEmpty(string pattern) => Assert.Empty(SimilarPaths.Suggest(Files, pattern));
}
