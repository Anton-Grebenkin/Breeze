using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeEditor.Modules.Terminal.Services.Build;

/// <summary>
/// Processes that hold build outputs (MSB3027/MSB3021 "The file is locked by: "MyApp (46980)""): usually the
/// app itself, still running from an earlier <c>dotnet run</c>. The build output is English
/// (<see cref="DotNetEnvironment"/>).
/// </summary>
public static partial class LockedFiles
{
    public static IReadOnlyList<LockingProcess> Of(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return [.. LockedBy().Matches(output)
            .SelectMany(static match => Process().Matches(match.Groups["list"].Value))
            .Select(static process => new LockingProcess(process.Groups["name"].Value.Trim(), int.Parse(process.Groups["id"].Value, CultureInfo.InvariantCulture)))
            .Distinct()];
    }

    [GeneratedRegex(@"The file is locked by: ""(?<list>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex LockedBy();

    [GeneratedRegex(@"(?<name>[^,(]+?) \((?<id>\d+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex Process();
}
