using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Core.Logging;

/// <summary>
/// Log file in the user data folder (<c>logs/</c>). Writing happens on a background thread via a queue: the caller
/// (including the UI thread) only enqueues. Batches are written and flushed immediately, so after a crash the file
/// has everything up to the last batch. When the queue is full or the disk is unavailable, entries are dropped and
/// their count is logged as soon as writing works again.
/// </summary>
public sealed class LogFileWriter : ILogFiles, IDisposable
{
    public const string FolderName = "logs";

    private const int QueueCapacity = 10_000;
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(3);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Channel<LogEntry> _queue = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(QueueCapacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    private readonly LogFileSet _files;
    private readonly string _sessionHeader;
    private readonly TimeProvider _time;
    private readonly StringBuilder _buffer = new();
    private readonly Task _pump;
    private long _dropped;
    private int _buffered;
    private StreamWriter? _writer;
    private DateOnly _day;
    private int _part;
    private volatile string? _currentFile;

    /// <param name="sessionHeader">Version, environment and process; heads every file of this run.</param>
    public LogFileWriter(string folder, string sessionHeader, TimeProvider time, LogFileLimits? limits = null)
    {
        _files = new LogFileSet(folder, limits ?? LogFileLimits.Default);
        _sessionHeader = sessionHeader;
        _time = time;
        _pump = Task.Run(PumpAsync);
    }

    public string Folder => _files.Folder;

    public string? CurrentFile => _currentFile;

    /// <summary>Callable from any thread; never blocks.</summary>
    public void Write(in LogEntry entry)
    {
        if (!_queue.Writer.TryWrite(entry))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    /// <summary>Drains the queue and closes the file (on exit and on crash).</summary>
    /// <returns><c>false</c> if not finished within <paramref name="timeout"/>.</returns>
    public bool Complete(TimeSpan timeout)
    {
        _queue.Writer.TryComplete();
        return _pump.Wait(timeout);
    }

    public void Dispose() => Complete(DisposeTimeout);

    private async Task PumpAsync()
    {
        TryDeleteExpired();
        var reader = _queue.Reader;
        try
        {
            FlushBuffer();
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var entry))
                {
                    LogLineFormatter.Append(_buffer, entry);
                    _buffered++;
                }

                AppendDroppedNotice();
                FlushBuffer();
            }
        }
        finally
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void TryDeleteExpired()
    {
        try
        {
            _files.DeleteExpired(_time.GetUtcNow());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is optional; try again on the next run.
        }
    }

    private void AppendDroppedNotice()
    {
        var dropped = Interlocked.Exchange(ref _dropped, 0);
        if (dropped > 0)
        {
            var message = string.Create(CultureInfo.InvariantCulture, $"Dropped log entries: {dropped} (queue overflow or the file was unavailable)");
            LogLineFormatter.Append(_buffer, new LogEntry(_time.GetLocalNow(), LogLevel.Warning, nameof(LogFileWriter), message, Environment.CurrentManagedThreadId));
        }
    }

    /// <summary>Writes and flushes the buffer, opening the file if needed (at startup only the header).</summary>
    private void FlushBuffer()
    {
        try
        {
            if (EnsureFile() is not { } writer)
            {
                Interlocked.Add(ref _dropped, _buffered);
                return;
            }

            writer.Write(_buffer);
            writer.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Disk full or folder unavailable: the batch is lost; the file reopens on the next one.
            Interlocked.Add(ref _dropped, _buffered);
            _writer?.Dispose();
            _writer = null;
        }
        finally
        {
            _buffer.Clear();
            _buffered = 0;
        }
    }

    // Switches to the next file on a new day or when the current one reaches the size limit.
    private StreamWriter? EnsureFile()
    {
        var now = _time.GetLocalNow();
        var day = DateOnly.FromDateTime(now.DateTime);
        if (_writer is not null && day == _day && _writer.BaseStream.Length < _files.Limits.MaxFileSize)
        {
            return _writer;
        }

        var fromPart = _writer is not null && day == _day ? _part + 1 : 0;
        _writer?.Dispose();
        _writer = null;
        if (_files.OpenNext(day, fromPart) is not var (stream, path, part))
        {
            return null;
        }

        _writer = new StreamWriter(stream, Utf8);
        (_day, _part, _currentFile) = (day, part, path);
        _writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"======== {now:yyyy-MM-dd HH:mm:ss.fff zzz} {_sessionHeader} ========"));
        return _writer;
    }
}
