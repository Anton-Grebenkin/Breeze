using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodeEditor.Agent.Eval;

/// <summary>
/// Copies a sample repository (a folder in <c>Fixtures</c>) for one run. Project files are stored with the
/// <c>.fixture</c> suffix so CodeEditor itself doesn't treat them as solution projects; the copy gets the real names,
/// then the task's bugs are planted.
/// </summary>
internal static partial class EvalRepository
{
    public const string FixtureSuffix = ".fixture";

    public static string HiddenFolder => Path.Combine(AppContext.BaseDirectory, "Hidden");

    public static string FixtureFolder(string fixture) => Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);

    /// <summary>Line of the <c>Calculator.Divide</c> declaration in the fixture (the "find" answer).</summary>
    public static int DivideLine() =>
        File.ReadAllLines(Path.Combine(FixtureFolder("Calc"), "src", "Calc", "Calculator.cs"))
            .Select((line, index) => (line, index))
            .First(pair => pair.line.Contains("public static double Divide(", StringComparison.Ordinal)).index + 1;

    /// <returns>The repository folder, ready for the agent's turn.</returns>
    /// <exception cref="InvalidOperationException">A seed wasn't found: the fixture and the task diverged.</exception>
    public static string Create(string folder, EvalTask task)
    {
        if (task.SourceRepo is { } external)
        {
            return Clone(external, folder);
        }

        var fixture = FixtureFolder(task.Fixture);
        foreach (var source in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(fixture, source);
            if (relative.EndsWith(FixtureSuffix, StringComparison.Ordinal))
            {
                relative = relative[..^FixtureSuffix.Length];
            }

            var target = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, File.ReadAllText(source).ReplaceLineEndings("\n"));
        }

        foreach (var (file, from, to) in task.Seeds)
        {
            Seed(folder, task, file, from.ReplaceLineEndings("\n"), to.ReplaceLineEndings("\n"));
        }

        foreach (var (file, text) in task.Files)
        {
            var target = Path.Combine(folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, text.ReplaceLineEndings("\n"));
        }

        return folder;
    }

    /// <summary>
    /// Clones an external repository at its current commit without remotes, so the agent can't push to the source and
    /// the source stays unchanged.
    /// </summary>
    private static string Clone(string source, string folder)
    {
        Git(null, "-c", "core.longpaths=true", "clone", "--quiet", "--local", source, folder);
        Git(folder, "remote", "remove", "origin");
        return folder;
    }

    /// <exception cref="InvalidOperationException">git failed.</exception>
    private static void Git(string? workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardError = true, WorkingDirectory = workingDirectory ?? string.Empty };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git не запустился.");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error}");
        }
    }

    /// <summary>Counts tests in the task's test project by <c>[Fact]</c> and <c>[Theory]</c> attributes.</summary>
    public static int CountTests(string folder, EvalTask task)
    {
        var project = Path.Combine(folder, task.TestsProject);
        return Directory.Exists(project)
            ? Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories).Sum(file => TestAttribute().Count(File.ReadAllText(file)))
            : 0;
    }

    private static void Seed(string folder, EvalTask task, string file, string from, string to)
    {
        var path = Path.Combine(folder, file);
        var text = File.ReadAllText(path);
        if (!text.Contains(from, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Задача {task.Id}: в {file} нет «{from}».");
        }

        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\[(Fact|Theory)\b")]
    private static partial Regex TestAttribute();
}
