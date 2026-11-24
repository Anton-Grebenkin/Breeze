using System.Text.RegularExpressions;
using CodeEditor.Architecture.Tests.Infrastructure;

namespace CodeEditor.Architecture.Tests;

/// <summary>
/// A folder's <c>.breeze/settings.json</c> comes with someone else's repository, so it is read by allow-list: every
/// settings section states its scope, and the sections a folder may set are the reviewed list below.
/// </summary>
public sealed partial class SettingsScopeTests
{
    /// <summary>
    /// A section goes here only if no value in it reaches outside the folder: nothing that runs without asking, no
    /// addresses, no registry. Keys that do are closed one by one with <c>AddUserOnlySetting</c>.
    /// </summary>
    private static readonly string[] FolderSections = ["AgentOptions", "EditorOptions", "FilesOptions", "SearchSettings", "TerminalOptions", "WindowOptions", "WorkbenchOptions"];

    [Fact]
    public void EverySection_StatesItsScope_AndFolderSectionsAreTheReviewedOnes()
    {
        var calls = RepositoryPaths.SourceFiles().SelectMany(path => File.ReadLines(path).Where(line => line.Contains("services.AddSettingsSection<", StringComparison.Ordinal))).ToList();
        var registered = calls.Select(line => Registration().Match(line)).ToList();

        Assert.NotEmpty(calls);
        Assert.All(registered, match => Assert.True(match.Success, "У секции настроек область задаётся явно: AddSettingsSection<T>(T.Section, SettingScope.User | Folder)."));
        Assert.Equal(
            FolderSections,
            registered.Where(match => match.Groups["scope"].Value == "Folder").Select(match => match.Groups["type"].Value).Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"AddSettingsSection<(?<type>\w+)>\(\k<type>\.Section, SettingScope\.(?<scope>User|Folder)\)")]
    private static partial Regex Registration();
}
