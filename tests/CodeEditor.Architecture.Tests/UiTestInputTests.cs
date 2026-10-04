using System.Text.RegularExpressions;
using CodeEditor.Architecture.Tests.Infrastructure;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// UI tests send input only through the guard (<c>Infrastructure/InputGuard</c>): a direct FlaUI key press or click
/// goes to whatever window is on top of the app and could, for example, close the user's browser tab.
/// </summary>
public sealed partial class UiTestInputTests
{
    [Fact]
    public void UiTests_SendInputOnlyThroughGuard()
    {
        var folder = Path.Combine(RepositoryPaths.Root, "tests", "CodeEditor.UI.Tests");
        var infrastructure = Path.Combine(folder, "Infrastructure") + Path.DirectorySeparatorChar;
        var violations = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(infrastructure, StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(entry => DirectInput().IsMatch(entry.line))
            .Select(entry => $"{RepositoryPaths.Relative(entry.path)}:{entry.index + 1}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(violations.Length == 0,
            "Ввод в UI-тестах — через Keyboard и Guarded*Click из Infrastructure:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [GeneratedRegex(@"using FlaUI\.Core\.Input;|FlaUI\.Core\.Input\.|\bMouse\.|(?<!Guarded|GuardedMouse)\.(Click|DoubleClick|RightClick)\(")]
    private static partial Regex DirectInput();
}
