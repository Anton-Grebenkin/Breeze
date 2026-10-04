namespace CodeEditor.Modules.Git.Services.Parsing;

/// <summary>A commit in the history.</summary>
/// <param name="Refs">Branches and tags as in <c>git log --decorate</c>: "HEAD -> main, origin/main, tag: v1.0".</param>
public sealed record GitCommitInfo(string Hash, string ShortHash, string Author, DateTimeOffset Date, string Refs, string Subject, string Body);
