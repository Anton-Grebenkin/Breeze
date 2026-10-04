using System.Text;
using System.Text.Json;
using CodeEditor.Core.Files;
using CodeEditor.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Service models for the model manager: the chat model list (<see cref="ChatModelFilter"/>) cached in the user folder,
/// so the manager opens at once with the previous list while a fresh one loads. Without network or key the cache
/// remains, and without a cache the <see cref="ModelCatalog.DefaultModels"/> catalog.
/// </summary>
public sealed partial class ModelDirectory(
    IOptionsMonitor<AgentOptions> options,
    IModelListClient client,
    IFileSystem fileSystem,
    UserDataPaths paths,
    ILogger<ModelDirectory> logger)
{
    public const string CacheFileName = "agent-models.json";

    private string CachePath => paths.File(CacheFileName);

    /// <summary>The previous list or the catalog, without network access.</summary>
    public IReadOnlyList<string> Cached()
    {
        try
        {
            if (fileSystem.FileExists(CachePath)
                && JsonSerializer.Deserialize(fileSystem.ReadAllText(CachePath), AgentJsonContext.Default.StringArray) is { Length: > 0 } cached)
            {
                return cached;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogCacheUnreadable(logger, CachePath, exception);
        }

        return ModelCatalog.DefaultModels;
    }

    /// <summary>A fresh list from the service, saved to the cache.</summary>
    /// <exception cref="AgentConfigurationException">No API key, or the endpoint is not a URL.</exception>
    /// <exception cref="System.ClientModel.ClientResultException">The service returned an error.</exception>
    public async Task<IReadOnlyList<string>> RefreshAsync(CancellationToken cancellationToken)
    {
        var models = ChatModelFilter.Chat(await client.ListAsync(options.CurrentValue, cancellationToken));
        try
        {
            fileSystem.WriteAllBytesAtomic(CachePath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize([.. models], AgentJsonContext.Default.StringArray)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogCacheUnwritable(logger, CachePath, exception);
        }

        return models;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model list cache {Path} is unreadable")]
    private static partial void LogCacheUnreadable(ILogger logger, string path, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model list cache {Path} was not written")]
    private static partial void LogCacheUnwritable(ILogger logger, string path, Exception exception);
}
