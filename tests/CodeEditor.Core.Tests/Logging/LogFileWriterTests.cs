using CodeEditor.Core.Files;
using CodeEditor.Core.Logging;
using CodeEditor.Testing;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Tests.Logging;

/// <summary>Uses a real folder on disk to check file sharing and cleanup of old files.</summary>
public sealed class LogFileWriterTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CodeEditor.Tests", "logs-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTimeProvider _time = new();

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Entries_AreWrittenAfterHeader()
    {
        var writer = Create();
        writer.Write(Entry("первая"));
        writer.Write(Entry("вторая"));

        Assert.True(writer.Complete(Timeout));

        var lines = File.ReadAllLines(writer.CurrentFile!);
        Assert.StartsWith("========", lines[0], StringComparison.Ordinal);
        Assert.Contains("CodeEditor test", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("Test: первая", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("Test: вторая", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void File_IsReadableByEditorWhileWriting()
    {
        using var writer = Create();
        writer.Write(Entry("пишется"));

        Assert.True(SpinWait.SpinUntil(() => writer.CurrentFile is { } file && new PhysicalFileSystem().ReadAllText(file).Contains("пишется", StringComparison.Ordinal), Timeout));
    }

    [Fact]
    public void FullFile_RollsToNextPart()
    {
        var writer = Create(new LogFileLimits(MaxFileSize: 200, MaxFilesPerDay: 3, RetentionDays: 7));
        for (var i = 0; i < 3; i++)
        {
            WriteFlushed(writer, $"{i}:" + new string('x', 100));
        }

        // The daily limit is reached: further entries are dropped and no new files appear.
        writer.Write(Entry("лишняя"));
        Assert.True(writer.Complete(Timeout));

        var files = Directory.GetFiles(_folder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(3, files.Count);
        Assert.EndsWith("-2.log", writer.CurrentFile, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondInstance_WritesToNextPart()
    {
        using var first = Create();
        using var second = Create();

        Assert.True(SpinWait.SpinUntil(() => first.CurrentFile is not null && second.CurrentFile is not null, Timeout));
        Assert.NotEqual(first.CurrentFile, second.CurrentFile);
    }

    [Fact]
    public void NewDay_StartsNewFile()
    {
        var writer = Create();
        writer.Write(Entry("сегодня"));
        Assert.True(SpinWait.SpinUntil(() => writer.CurrentFile is not null, Timeout));
        var today = writer.CurrentFile;

        _time.Advance(TimeSpan.FromDays(1));
        writer.Write(Entry("завтра"));
        Assert.True(writer.Complete(Timeout));

        Assert.NotEqual(today, writer.CurrentFile);
        Assert.Contains("завтра", File.ReadAllText(writer.CurrentFile!), StringComparison.Ordinal);
    }

    [Fact]
    public void ExpiredFiles_AreDeletedAtStart()
    {
        Directory.CreateDirectory(_folder);
        var old = Path.Combine(_folder, "codeeditor-20250101.log");
        var foreign = Path.Combine(_folder, "notes.txt");
        File.WriteAllText(old, "старый");
        File.WriteAllText(foreign, "чужой");
        var recent = Path.Combine(_folder, "codeeditor-20250201.log");
        File.WriteAllText(recent, "три недели назад");
        File.SetLastWriteTimeUtc(old, _time.GetUtcNow().UtcDateTime.AddDays(-31));
        File.SetLastWriteTimeUtc(recent, _time.GetUtcNow().UtcDateTime.AddDays(-21));
        File.SetLastWriteTimeUtc(foreign, _time.GetUtcNow().UtcDateTime.AddDays(-60));

        Assert.True(Create().Complete(Timeout));

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(foreign));
    }

    // A month of verbose logs must not fill the disk: past the total size limit the oldest files go first.
    [Fact]
    public void FilesOverTheTotalSizeLimit_AreDeletedOldestFirst()
    {
        Directory.CreateDirectory(_folder);
        string[] files = [.. Enumerable.Range(1, 4).Select(day => Path.Combine(_folder, $"codeeditor-2025030{day}.log"))];
        for (var index = 0; index < files.Length; index++)
        {
            File.WriteAllText(files[index], new string('x', 1_000));
            File.SetLastWriteTimeUtc(files[index], _time.GetUtcNow().UtcDateTime.AddDays(index - files.Length));
        }

        Assert.True(Create(new LogFileLimits(MaxFileSize: 5_000, MaxFilesPerDay: 3, RetentionDays: 30, MaxTotalSize: 2_500)).Complete(Timeout));

        Assert.Equal([false, false, true, true], files.Select(File.Exists));
    }

    [Fact]
    public void Provider_FiltersByLevelSwitch_AndAddsScopes()
    {
        var writer = Create();
        var levels = new LogLevelSwitch(LogLevel.Information);
        using var provider = new FileLoggerProvider(writer, levels, _time);
        var logger = provider.CreateLogger("CodeEditor.Core.Commands.CommandService");

        logger.LogDebug("скрыто");
        using (logger.BeginScope("чат 42"))
        {
            logger.LogInformation("видно");
        }

        levels.Minimum = LogLevel.Debug;
        logger.LogDebug("теперь видно");
        Assert.True(writer.Complete(Timeout));

        var text = File.ReadAllText(writer.CurrentFile!);
        Assert.DoesNotContain("скрыто", text, StringComparison.Ordinal);
        Assert.Contains("[INF] #", text, StringComparison.Ordinal);
        Assert.Contains("CommandService: видно {чат 42}", text, StringComparison.Ordinal);
        Assert.Contains("[DBG]", text, StringComparison.Ordinal);
    }

    private LogFileWriter Create(LogFileLimits? limits = null) => new(_folder, "CodeEditor test", _time, limits);

    private LogEntry Entry(string message) => new(_time.GetLocalNow(), LogLevel.Information, "Test", message, Environment.CurrentManagedThreadId);

    // One entry per batch: wait until it is in the file, otherwise rotation is nondeterministic.
    private void WriteFlushed(LogFileWriter writer, string message)
    {
        writer.Write(Entry(message));
        Assert.True(SpinWait.SpinUntil(() => writer.CurrentFile is { } file && new PhysicalFileSystem().ReadAllText(file).Contains(message, StringComparison.Ordinal), Timeout));
    }
}
