using System.Globalization;

namespace CodeEditor.Core.Logging;

/// <summary>
/// Log files in a folder: <c>codeeditor-20260927.log</c>, further parts of a day <c>codeeditor-20260927-1.log</c>.
/// Picks a part with free space and deletes old files.
/// </summary>
internal sealed class LogFileSet(string folder, LogFileLimits limits)
{
    public const string Prefix = "codeeditor-";
    public const string Extension = ".log";

    public string Folder => folder;

    public LogFileLimits Limits => limits;

    public string PathFor(DateOnly day, int part)
    {
        var suffix = part == 0 ? string.Empty : "-" + part.ToString(CultureInfo.InvariantCulture);
        return Path.Combine(folder, Prefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + suffix + Extension);
    }

    /// <summary>
    /// Opens for append the first part of the day, from <paramref name="fromPart"/> on, that has space and is not
    /// being written by another editor instance (writes are exclusive, reads are not); <c>null</c> when the daily
    /// limit is reached.
    /// </summary>
    public (FileStream Stream, string Path, int Part)? OpenNext(DateOnly day, int fromPart)
    {
        Directory.CreateDirectory(folder);
        for (var part = fromPart; part < limits.MaxFilesPerDay; part++)
        {
            var path = PathFor(day, part);
            if (File.Exists(path) && new FileInfo(path).Length >= limits.MaxFileSize)
            {
                continue;
            }

            try
            {
                return (new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read | FileShare.Delete), path, part);
            }
            catch (IOException)
            {
                // Another editor instance is writing this file; take the next part.
            }
        }

        return null;
    }

    /// <summary>Deletes log files not modified within the retention period; skips files in use.</summary>
    public void DeleteExpired(DateTimeOffset now)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var cutoff = now.UtcDateTime - TimeSpan.FromDays(limits.RetentionDays);
        foreach (var file in Directory.EnumerateFiles(folder, Prefix + "*" + Extension))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Held by another editor instance; it will be deleted next time.
            }
        }
    }
}
