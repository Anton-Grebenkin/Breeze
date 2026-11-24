using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace CodeEditor.Core.Processes;

/// <summary>
/// Reads a redirected process stream line by line as bytes. A line is UTF-8 when it is valid UTF-8, otherwise the
/// console (OEM) code page: a program that fails before switching its output to UTF-8 — a PowerShell parse error, a
/// script blocked by antivirus — is still readable. Both encodings keep a line feed as one byte, so lines are split
/// before decoding. O(n) in output length.
/// </summary>
internal static class ProcessOutputReader
{
    private const int BufferSize = 16 * 1024;
    private const byte LineFeed = (byte)'\n';
    private const byte CarriageReturn = (byte)'\r';
    private const char ByteOrderMark = '﻿';

    private static readonly Lazy<Encoding> ConsoleEncoding = new(ResolveConsoleEncoding);

    /// <summary>Calls <paramref name="onLine"/> for every line until the stream ends or its pipe breaks.</summary>
    public static async Task ReadLinesAsync(Stream stream, Action<string> onLine)
    {
        var buffer = new byte[BufferSize];
        using var pending = new MemoryStream();
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                Split(buffer.AsSpan(0, read), pending, onLine);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // The process was killed or disposed: what has been read stays.
        }

        if (pending.Length > 0)
        {
            onLine(Decode(pending.GetBuffer().AsSpan(0, (int)pending.Length), ConsoleEncoding.Value));
        }
    }

    /// <summary>Decodes one line without its line feed: UTF-8 if valid, otherwise <paramref name="fallback"/>.</summary>
    internal static string Decode(ReadOnlySpan<byte> line, Encoding fallback)
    {
        if (!line.IsEmpty && line[^1] == CarriageReturn)
        {
            line = line[..^1];
        }

        var text = Utf8.IsValid(line) ? Encoding.UTF8.GetString(line) : fallback.GetString(line);
        return text.Length > 0 && text[0] == ByteOrderMark ? text[1..] : text;
    }

    private static void Split(ReadOnlySpan<byte> chunk, MemoryStream pending, Action<string> onLine)
    {
        while (chunk.IndexOf(LineFeed) is var end and >= 0)
        {
            if (pending.Length == 0)
            {
                onLine(Decode(chunk[..end], ConsoleEncoding.Value));
            }
            else
            {
                pending.Write(chunk[..end]);
                onLine(Decode(pending.GetBuffer().AsSpan(0, (int)pending.Length), ConsoleEncoding.Value));
                pending.SetLength(0);
            }

            chunk = chunk[(end + 1)..];
        }

        pending.Write(chunk);
    }

    // A windowless child gets a console with the system OEM code page, whatever the UI language of the editor is.
    private static Encoding ResolveConsoleEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return OperatingSystem.IsWindows() ? Encoding.GetEncoding((int)GetOEMCP()) : Encoding.Latin1;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetOEMCP();
}
