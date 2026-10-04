namespace CodeEditor.Benchmarks;

/// <summary>
/// Deterministic set of paths resembling a C# repository.
/// </summary>
internal static class SamplePaths
{
    private const int Seed = 42;

    private static readonly string[] Folders =
        ["src", "tests", "Platform", "Modules", "CodeEditor.Core", "Services", "ViewModels", "Views", "Commands", "Infrastructure"];

    private static readonly string[] Words =
        ["Main", "Window", "View", "Model", "Command", "Palette", "Service", "Registry", "Explorer", "Search", "Document", "Editor"];

    private static readonly string[] Extensions = [".cs", ".xaml", ".json", ".md"];

    public static string[] Generate(int count)
    {
        var random = new Random(Seed);
        var paths = new string[count];

        for (var i = 0; i < count; i++)
        {
            var folder = string.Join('/', Enumerable.Range(0, random.Next(2, 5)).Select(_ => Pick(random, Folders)));
            var name = string.Concat(Enumerable.Range(0, random.Next(1, 4)).Select(_ => Pick(random, Words)));
            paths[i] = $"{folder}/{name}{Pick(random, Extensions)}";
        }

        return paths;
    }

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
}
