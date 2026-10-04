using CodeEditor.Modules.TextEditor.Languages;
using CodeEditor.Modules.TextEditor.Services;

namespace CodeEditor.Modules.TextEditor.Tests;

/// <summary>Language catalog (ADR 0036): file name, pattern, extension, status bar name, Markdown fences.</summary>
public sealed class LanguageCatalogTests
{
    [Theory]
    [InlineData(@"C:\repo\Dockerfile", "dockerfile")]
    [InlineData(@"C:\repo\DOCKERFILE", "dockerfile")]
    [InlineData(@"C:\repo\Containerfile", "dockerfile")]
    [InlineData(@"C:\repo\Makefile", "makefile")]
    [InlineData(@"C:\repo\makefile", "makefile")]
    [InlineData(@"C:\repo\GNUmakefile", "makefile")]
    [InlineData(@"C:\repo\CMakeLists.txt", "cmake")]
    [InlineData(@"C:\repo\Jenkinsfile", "groovy")]
    [InlineData(@"C:\repo\Gemfile", "ruby")]
    [InlineData(@"C:\repo\Rakefile", "ruby")]
    [InlineData(@"C:\repo\.gitignore", "ignore")]
    [InlineData(@"C:\repo\.dockerignore", "ignore")]
    [InlineData(@"C:\repo\.npmignore", "ignore")]
    [InlineData(@"C:\repo\.gitattributes", "gitattributes")]
    [InlineData(@"C:\repo\.env", "dotenv")]
    [InlineData(@"C:\repo\.editorconfig", "editorconfig")]
    [InlineData(@"C:\repo\.gitconfig", "ini")]
    [InlineData(@"C:\repo\.bashrc", "shellscript")]
    [InlineData(@"C:\repo\Cargo.lock", "toml")]
    public void ExactFileName_SelectsLanguage(string path, string id) => Assert.Equal(id, LanguageCatalog.ForFile(path)?.Id);

    [Theory]
    [InlineData("Dockerfile.dev", "dockerfile")]
    [InlineData("Dockerfile.md", "dockerfile")]
    [InlineData("api.dockerfile", "dockerfile")]
    [InlineData("Containerfile.prod", "dockerfile")]
    [InlineData(".env.local", "dotenv")]
    [InlineData(".env.production", "dotenv")]
    public void Pattern_SelectsLanguage_BeforeExtension(string name, string id) => Assert.Equal(id, LanguageCatalog.ForFile(name)?.Id);

    [Theory]
    [InlineData("app.ts", "typescript")]
    [InlineData("app.mts", "typescript")]
    [InlineData("app.cts", "typescript")]
    [InlineData("App.tsx", "typescriptreact")]
    [InlineData("App.jsx", "javascriptreact")]
    [InlineData("index.cjs", "javascript")]
    [InlineData("ci.yml", "yaml")]
    [InlineData("build.sh", "shellscript")]
    [InlineData("run.cmd", "bat")]
    [InlineData("setup.cfg", "ini")]
    [InlineData("nginx.conf", "ini")]
    [InlineData("app.properties", "properties")]
    [InlineData("prod.env", "dotenv")]
    [InlineData("pyproject.toml", "toml")]
    [InlineData("Program.fsx", "fsharp")]
    [InlineData("main.go", "go")]
    [InlineData("main.rs", "rust")]
    [InlineData("build.gradle.kts", "kotlin")]
    [InlineData("app.rb", "ruby")]
    [InlineData("init.lua", "lua")]
    [InlineData("App.swift", "swift")]
    [InlineData("main.dart", "dart")]
    [InlineData("api.proto", "proto")]
    [InlineData("schema.gql", "graphql")]
    [InlineData("flow.mermaid", "mermaid")]
    [InlineData("requests.http", "http")]
    [InlineData("requests.rest", "http")]
    [InlineData("server.log", "log")]
    [InlineData("deps.cmake", "cmake")]
    [InlineData("rules.mk", "makefile")]
    [InlineData("site.less", "less")]
    [InlineData("App.vue", "vue")]
    [InlineData("App.svelte", "svelte")]
    [InlineData("config.json5", "json5")]
    [InlineData("events.jsonl", "jsonl")]
    [InlineData("site.webmanifest", "json")]
    [InlineData("page.mdx", "mdx")]
    public void Extension_SelectsLanguage(string name, string id) => Assert.Equal(id, LanguageCatalog.ForFile(name)?.Id);

    [Theory]
    [InlineData(@"C:\repo\readme")]
    [InlineData(@"C:\repo\notes.txt")]
    [InlineData(@"C:\repo\data.bin")]
    [InlineData(@"C:\repo\Dockerfiles")]
    public void UnknownFile_HasNoLanguage(string path) => Assert.Null(LanguageCatalog.ForFile(path));

    [Theory]
    [InlineData(@"C:\src\App.tsx", "TypeScript JSX")]
    [InlineData(@"C:\src\Dockerfile", "Dockerfile")]
    [InlineData(@"C:\src\.gitignore", "Ignore")]
    [InlineData(@"C:\src\deploy.yaml", "YAML")]
    [InlineData(@"C:\src\build.sh", "Shell Script")]
    [InlineData(@"C:\src\CMakeLists.txt", "CMake")]
    [InlineData(@"C:\src\notes.txt", "Обычный текст")]
    public void StatusBarName_ComesFromCatalog(string path, string name) => Assert.Equal(name, LanguageNames.ForFile(path));

    [Theory]
    [InlineData("typescript", "typescript")]
    [InlineData("ts", "typescript")]
    [InlineData("TSX", "typescriptreact")]
    [InlineData("bash", "shellscript")]
    [InlineData("shell", "shellscript")]
    [InlineData("golang", "go")]
    [InlineData("rs", "rust")]
    [InlineData("c#", "csharp")]
    [InlineData("Dockerfile", "dockerfile")]
    [InlineData("gitignore", "ignore")]
    [InlineData("make", "makefile")]
    [InlineData("yml", "yaml")]
    public void CodeFenceAlias_SelectsLanguage(string alias, string id) => Assert.Equal(id, LanguageCatalog.ForAlias(alias)?.Id);

    [Fact]
    public void UnknownCodeFence_HasNoLanguage() => Assert.Null(LanguageCatalog.ForAlias("text"));

    [Fact]
    public void Keys_AreUniqueAcrossLanguages()
    {
        var languages = LanguageCatalog.All;

        AssertUnique(languages.Select(language => language.Id).Concat(languages.SelectMany(language => language.Aliases)));
        AssertUnique(languages.SelectMany(language => language.FileNames));
        AssertUnique(languages.SelectMany(language => language.Extensions));
        AssertUnique(languages.SelectMany(language => language.FilePatterns));
    }

    [Fact]
    public void Keys_AreWellFormed()
    {
        var languages = LanguageCatalog.All;

        Assert.All(languages.SelectMany(language => language.Extensions), extension => Assert.Matches(@"^\.[\w.+-]+$", extension));
        Assert.All(languages.SelectMany(language => language.FilePatterns), pattern => Assert.Contains('*', pattern));
        Assert.All(languages.SelectMany(language => language.FileNames), name => Assert.DoesNotContain('*', name));
        Assert.All(languages, language => Assert.False(string.IsNullOrWhiteSpace(language.Highlighting)));
    }

    private static void AssertUnique(IEnumerable<string> keys)
    {
        var duplicates = keys.GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();

        Assert.True(duplicates.Length == 0, $"Повторы: {string.Join(", ", duplicates)}.");
    }
}
