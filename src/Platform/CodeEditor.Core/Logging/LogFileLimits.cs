namespace CodeEditor.Core.Logging;

/// <summary>
/// On-disk log limits: one file per day, then further parts (<c>-1</c>, <c>-2</c>…) up to
/// <see cref="MaxFilesPerDay"/>. At startup files older than <see cref="RetentionDays"/> days are deleted, then the
/// oldest ones until the folder fits <see cref="MaxTotalSize"/>. Defaults: at most 25 MB per day, a month of history
/// (a week was too short to look into a problem found later), 200 MB in all.
/// </summary>
public readonly record struct LogFileLimits(long MaxFileSize, int MaxFilesPerDay, int RetentionDays, long MaxTotalSize = LogFileLimits.DefaultTotalSize)
{
    public const long DefaultTotalSize = 200L * 1024 * 1024;

    public static LogFileLimits Default { get; } = new(MaxFileSize: 5 * 1024 * 1024, MaxFilesPerDay: 5, RetentionDays: 30);
}
