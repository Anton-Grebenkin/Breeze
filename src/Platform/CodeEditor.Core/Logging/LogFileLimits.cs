namespace CodeEditor.Core.Logging;

/// <summary>
/// On-disk log limits: one file per day, then further parts (<c>-1</c>, <c>-2</c>…) up to
/// <see cref="MaxFilesPerDay"/>; files older than <see cref="RetentionDays"/> days are deleted at startup.
/// Defaults: at most 25 MB per day and a week of history.
/// </summary>
public readonly record struct LogFileLimits(long MaxFileSize, int MaxFilesPerDay, int RetentionDays)
{
    public static LogFileLimits Default { get; } = new(MaxFileSize: 5 * 1024 * 1024, MaxFilesPerDay: 5, RetentionDays: 7);
}
