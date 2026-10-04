using System.ClientModel.Primitives;
using System.Globalization;
using System.Text;
using CodeEditor.Core.Logging;
using CodeEditor.Core.Storage;

namespace CodeEditor.Modules.Agent.Services.Http;

/// <summary>
/// Model traffic log (<c>agent.trafficLog</c>): one file per request with the URL, the request body after all edits and
/// the raw response; event streams are written as they are read. Headers are not logged: they hold the API key. Used to
/// see the raw response when the library misreads it, and for support requests to the service. Sits closest to the
/// transport, so it sees exactly what went over the wire. Logs both the agent conversation and service requests
/// (web search).
/// </summary>
internal sealed class TrafficLogPolicy(string folder, TimeProvider time) : PipelinePolicy
{
    /// <summary>Subfolder inside the application log folder.</summary>
    public const string FolderName = "model-traffic";

    private int _requests;

    /// <summary>The log policy for the request pipeline; <c>null</c> when logging is off.</summary>
    public static TrafficLogPolicy? For(AgentOptions options, UserDataPaths paths, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(paths);
        return options.TrafficLog ? new TrafficLogPolicy(Path.Combine(paths.File(LogFileWriter.FolderName), FolderName), time) : null;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        var log = Start(message);
        ProcessNext(message, pipeline, currentIndex);
        Finish(message, log);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        var log = Start(message);
        await ProcessNextAsync(message, pipeline, currentIndex);
        Finish(message, log);
    }

    private FileStream Start(PipelineMessage message)
    {
        Directory.CreateDirectory(folder);
        var name = string.Create(CultureInfo.InvariantCulture, $"{time.GetLocalNow():yyyyMMdd-HHmmss-fff}-{Interlocked.Increment(ref _requests)}.txt");
        var log = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        Write(log, $"{message.Request.Method} {message.Request.Uri}\n\n");
        message.Request.Content?.WriteTo(log);
        return log;
    }

    // A buffered response is written at once, a stream as the client reads it; the file closes with the stream.
    private static void Finish(PipelineMessage message, FileStream log)
    {
        if (message.Response is not { } response)
        {
            log.Dispose();
            return;
        }

        Write(log, $"\n\n--- {response.Status}\n\n");
        if (message.BufferResponse || response.ContentStream is null)
        {
            log.Write(response.Content.ToMemory().Span);
            log.Dispose();
            return;
        }

        response.ContentStream = new TeeReadStream(response.ContentStream, log);
    }

    private static void Write(Stream log, string text) => log.Write(Encoding.UTF8.GetBytes(text));
}
