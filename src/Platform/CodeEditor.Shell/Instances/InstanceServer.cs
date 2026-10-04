using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CodeEditor.Shell.Instances;

/// <summary>
/// Serves requests from other launches over this window's pipe, one at a time, until stopped. The request is
/// acknowledged before it is handled, so the sender exits at once.
/// </summary>
public sealed partial class InstanceServer(ILogger<InstanceServer> logger)
{
    public async Task RunAsync(string pipeName, Func<InstanceRequest, Task> handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var request = await InstanceProtocol.ReadRequestAsync(pipe, cancellationToken).ConfigureAwait(false);
                await InstanceProtocol.WriteAcknowledgementAsync(pipe, cancellationToken).ConfigureAwait(false);
                await handle(request).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                LogRequestFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A request from another launch failed")]
    private static partial void LogRequestFailed(ILogger logger, Exception exception);
}
