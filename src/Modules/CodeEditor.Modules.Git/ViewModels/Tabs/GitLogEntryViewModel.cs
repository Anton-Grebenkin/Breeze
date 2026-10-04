using System.Globalization;
using CodeEditor.Modules.Git.Services.Parsing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeEditor.Modules.Git.ViewModels.Tabs;

/// <summary>
/// A commit in the history list. Immutable, yet an <see cref="ObservableObject"/>: otherwise WPF bindings subscribe via
/// PropertyDescriptor and keep the rows alive in memory.
/// </summary>
public sealed class GitLogEntryViewModel(GitCommitInfo commit) : ObservableObject
{
    public GitCommitInfo Commit { get; } = commit;

    public string Subject => Commit.Subject;

    public string Hash => Commit.Hash;

    public string ShortHash => Commit.ShortHash;

    public string Author => Commit.Author;

    /// <summary>Commit date and time in the user's time zone and format.</summary>
    public string DateText => Commit.Date == DateTimeOffset.MinValue ? string.Empty : Commit.Date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    /// <summary>Branches and tags on the commit: "HEAD -> main, origin/main".</summary>
    public string Refs => Commit.Refs;

    public bool HasRefs => Commit.Refs.Length > 0;

    /// <summary>The full message: subject and body.</summary>
    public string Message => Commit.Body.Length == 0 ? Commit.Subject : Commit.Subject + Environment.NewLine + Environment.NewLine + Commit.Body;

    public string AutomationId => $"Git.Commit.{Commit.ShortHash}";

    public override string ToString() => $"{Commit.ShortHash} {Commit.Subject}";
}
