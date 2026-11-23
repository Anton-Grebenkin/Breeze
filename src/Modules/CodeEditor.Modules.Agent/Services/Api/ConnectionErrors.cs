using System.ClientModel;
using System.Globalization;
using System.Net;
using System.Text.Json;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Why a connection's model list did not load, in words that say what to fix.</summary>
public static class ConnectionErrors
{
    /// <summary>The failures a model list request ends with; anything else is a programming error.</summary>
    public static bool IsExpected(Exception exception) =>
        exception is AgentConfigurationException or ClientResultException or HttpRequestException or IOException or JsonException or InvalidOperationException;

    public static string Describe(Exception exception, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            AgentConfigurationException configuration => configuration.Message,
            ClientResultException { Status: (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.Forbidden } => Strings.ConnectionKeyRejected,
            ClientResultException { Status: (int)HttpStatusCode.NotFound } => Format(Strings.ConnectionNoModelList, endpoint.TrimEnd('/') + "/models"),
            ClientResultException { Status: 0 } or HttpRequestException => Format(Strings.ErrorNoConnection, endpoint),
            ClientResultException result => Format(Strings.ErrorService, result.Status, ServiceErrorText.Of(result) ?? result.Message),
            JsonException => Strings.ConnectionNotModelList,
            _ => exception.Message,
        };
    }

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
