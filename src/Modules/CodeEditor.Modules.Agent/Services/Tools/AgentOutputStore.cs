using System.Globalization;
using System.Text;
using CodeEditor.Core.Files;
using CodeEditor.Modules.Agent.Contracts;
using CodeEditor.Modules.Agent.Resources;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Modules.Agent.Services.Tools;

/// <summary>
/// Long tool outputs go to <c>.breeze/agent/outputs/</c> (ADR 0012); the model gets the head and tail, line and
/// character counts and the path to the full text. Files older than three days are deleted when a new one is written.
/// Without an open folder or if the write fails, only the head and tail.
/// </summary>
public sealed partial class AgentOutputStore(IWorkspace workspace, IFileSystem fileSystem, TimeProvider time, ILogger<AgentOutputStore> logger) : IAgentOutputStore
{
    /// <summary>Outputs up to this many characters (~5K tokens) go to the model in full.</summary>
    public const int InlineLimit = 20_000;

    public const int HeadCharacters = 4_000;

    /// <summary>The tail is longer than the head: the end of an output holds the outcome and errors.</summary>
    public const int TailCharacters = 6_000;

    public const string FolderName = "outputs";

    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(3);

    private const string TimeFormat = "yyyyMMdd-HHmmss-fff";

    private int _sequence;

    public string Fit(string text, string toolName)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= InlineLimit)
        {
            return text;
        }

        var preview = ToolOutput.HeadAndTail(text, HeadCharacters, TailCharacters);
        var lines = text.AsSpan().Count('\n') + 1;
        var note = Save(text, toolName) is { } path
            ? Format(Strings.OutputSavedToFile, lines, text.Length, path)
            : Format(Strings.OutputNotSaved, lines, text.Length);
        return preview + "\n\n" + note;
    }

    /// <summary>Saves text to <c>.breeze/agent/outputs/</c>: a long output or the history before compaction.</summary>
    /// <param name="owner">Whose text this is; part of the file name after the timestamp.</param>
    /// <returns>The workspace-relative file path; <c>null</c> if saving failed.</returns>
    public string? Save(string text, string owner)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (AgentDataFolder.PathOf(workspace, FolderName) is not { } folder)
        {
            return null;
        }

        try
        {
            AgentDataFolder.Ensure(workspace, fileSystem, folder);
            var now = time.GetLocalNow().DateTime;
            Prune(folder, now);
            var name = string.Create(CultureInfo.InvariantCulture, $"{now.ToString(TimeFormat, CultureInfo.InvariantCulture)}-{Interlocked.Increment(ref _sequence)}-{owner}.txt");
            var file = Path.Combine(folder, name);
            fileSystem.WriteAllBytesAtomic(file, Encoding.UTF8.GetBytes(text));
            return workspace.RelativePath(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(logger, exception, folder);
            return null;
        }
    }

    // The file name starts with the write time; other files (without it) are left alone.
    private void Prune(string folder, DateTime now)
    {
        foreach (var entry in fileSystem.EnumerateEntries(folder).Where(static entry => !entry.IsDirectory && entry.Name.Length > TimeFormat.Length))
        {
            if (DateTime.TryParseExact(entry.Name.AsSpan(0, TimeFormat.Length), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var written)
                && now - written > KeepFor)
            {
                fileSystem.DeleteFile(entry.FullPath);
            }
        }
    }

    private static string Format(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save a tool output to {Folder}")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string folder);
}
