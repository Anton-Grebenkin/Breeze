using CodeEditor.Modules.Search.Services.Agent;

namespace CodeEditor.Modules.Search.Tests;

/// <summary>Hints for unproductive searching: a streak of empty results and a streak of searches in one file.</summary>
public sealed class SearchHintsTests
{
    [Fact]
    public void EmptyStreak_HintsOnce_ResetsOnHit()
    {
        var hints = new SearchHints();

        for (var i = 1; i < SearchHints.EmptyStreak; i++)
        {
            Assert.Equal(string.Empty, hints.After("*.cs", found: false));
        }

        Assert.Contains("поисков подряд ничего не нашли", hints.After("*.cs", found: false), StringComparison.Ordinal);
        Assert.Equal(string.Empty, hints.After("*.cs", found: false));
        Assert.Equal(string.Empty, hints.After("*.cs", found: true));
        Assert.Equal(string.Empty, hints.After("*.cs", found: false));
    }

    [Fact]
    public void SameFile_HintsToReadIt_NotForGlobs()
    {
        var hints = new SearchHints();
        const string file = "**/Catalog/Migrations/CatalogDbContextModelSnapshot.cs";

        Assert.Equal(string.Empty, hints.After(file, found: true));
        Assert.Equal(string.Empty, hints.After(file, found: true));
        Assert.Contains("прочитайте файл через read_file", hints.After(file, found: true), StringComparison.Ordinal);
        Assert.Equal(string.Empty, hints.After(file, found: true));

        Assert.Equal(string.Empty, hints.After("**/Migrations/*.cs", found: true));
        Assert.Equal(string.Empty, hints.After("**/Migrations/*.cs", found: true));
        Assert.Equal(string.Empty, hints.After("**/Migrations/*.cs", found: true));
    }
}
