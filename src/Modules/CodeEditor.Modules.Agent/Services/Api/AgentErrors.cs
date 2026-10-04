using System.ClientModel;
using System.Globalization;
using System.Net;
using CodeEditor.Modules.Agent.Resources;

namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>Turns model request errors into readable messages that say what to do.</summary>
public static class AgentErrors
{
    public static string Describe(Exception exception, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(options);

        return exception switch
        {
            AgentConfigurationException configuration => configuration.Message,
            ClientResultException { Status: (int)HttpStatusCode.Unauthorized } => Strings.ErrorApiKeyRejected,

            // 403 is not always about the key: Provod uses it to demand a first top-up and explains why in the body.
            ClientResultException { Status: (int)HttpStatusCode.Forbidden } forbidden when ServiceErrorText.Of(forbidden) is null => Strings.ErrorApiKeyRejected,
            ClientResultException { Status: (int)HttpStatusCode.NotFound } => Format(Strings.ErrorModelNotFound, options.Model, options.Endpoint),
            ClientResultException { Status: (int)HttpStatusCode.PaymentRequired } => Strings.ErrorPaymentRequired,
            ClientResultException { Status: (int)HttpStatusCode.TooManyRequests } => Strings.ErrorTooManyRequests,
            ClientResultException result => Format(Strings.ErrorService, result.Status, ServiceErrorText.Of(result) ?? result.Message),
            ModelStreamException stream => Format(Strings.ErrorStream, stream.Message),
            HttpRequestException => Format(Strings.ErrorNoConnection, options.Endpoint),
            _ => exception.Message,
        };
    }

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
