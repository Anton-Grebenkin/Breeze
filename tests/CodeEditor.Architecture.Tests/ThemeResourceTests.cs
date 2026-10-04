using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeEditor.Architecture.Tests.Infrastructure;
using CodeEditor.UI.Themes;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// Design system: themes are complete and consistent, and markup takes colors only from tokens.
/// </summary>
public sealed partial class ThemeResourceTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string ThemesFolder => Path.Combine(RepositoryPaths.Source, "Platform", "CodeEditor.UI", "Themes");

    [Fact]
    public void ColorDictionaries_DefineSameKeys()
    {
        var dark = KeysOf(Path.Combine(ThemesFolder, "Colors.Dark.xaml"));
        var light = KeysOf(Path.Combine(ThemesFolder, "Colors.Light.xaml"));

        Assert.Equal(dark.Order(StringComparer.Ordinal), light.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ThemeKeys_AreDefinedInColorDictionaries()
    {
        var defined = KeysOf(Path.Combine(ThemesFolder, "Colors.Dark.xaml"));

        var missing = typeof(ThemeKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(key => !defined.Contains(key))
            .ToArray();

        Assert.True(missing.Length == 0, $"В словарях цветов нет ключей: {string.Join(", ", missing)}.");
    }

    // Modules refer to icons by name (IconNames); a typo would show "?" instead of the icon.
    [Fact]
    public void IconNames_AreCodicons()
    {
        var unknown = typeof(CodeEditor.Shell.IconNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(name => !Codicons.Contains(name))
            .ToArray();

        Assert.True(unknown.Length == 0, $"Нет в Codicons: {string.Join(", ", unknown)}.");
    }

    [Fact]
    public void ResourceReferences_PointToDefinedKeys()
    {
        var files = XamlFiles().ToArray();
        var defined = files.SelectMany(KeysOf).ToHashSet(StringComparer.Ordinal);

        var unknown = files
            .SelectMany(file => ResourceReferencePattern().Matches(File.ReadAllText(file))
                .Select(match => (File: file, Key: match.Groups["key"].Value)))
            .Where(reference => !defined.Contains(reference.Key))
            .Select(reference => $"{RepositoryPaths.Relative(reference.File)}: {reference.Key}")
            .ToArray();

        Assert.True(unknown.Length == 0, $"Ссылки на неизвестные ресурсы: {string.Join("; ", unknown)}.");
    }

    [Fact]
    public void Markup_DoesNotContainColorLiterals()
    {
        var violations = XamlFiles()
            .Where(file => !Path.GetFileName(file).StartsWith("Colors.", StringComparison.Ordinal))
            .SelectMany(file => ColorLiteralPattern().Matches(File.ReadAllText(file))
                .Select(match => $"{RepositoryPaths.Relative(file)}: {match.Value}"))
            .ToArray();

        Assert.True(violations.Length == 0,
            $"Цвета в разметке — только через токены темы (DynamicResource): {string.Join("; ", violations)}.");
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(RepositoryPaths.Source, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> KeysOf(string path) =>
        XDocument.Load(path)
            .Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    // {DynamicResource Brush.Text.Primary} or {StaticResource Button.Subtle}; x:Static references are not checked.
    [GeneratedRegex(@"\{(?:Dynamic|Static)Resource\s+(?<key>[\w.]+)\s*\}")]
    private static partial Regex ResourceReferencePattern();

    // A hex color, or a named color in a brush property.
    [GeneratedRegex(@"""#[0-9A-Fa-f]{3,8}""|(?:Background|Foreground|BorderBrush|Fill|Stroke)=""(?!\{|Transparent"")[A-Za-z]+""")]
    private static partial Regex ColorLiteralPattern();
}
