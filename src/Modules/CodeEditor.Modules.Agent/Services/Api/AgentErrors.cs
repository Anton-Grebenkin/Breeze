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

            // Status 0: the request got no answer; the library's message is the raw .NET text.
            ClientResultException { Status: 0 } => Format(Strings.ErrorNoConnection, options.Endpoint),
            ClientResultException result => Format(Strings.ErrorService, result.Status, ServiceErrorText.Of(result) ?? result.Message),
            ModelStreamException stream => Format(Strings.ErrorStream, stream.Message),
            HttpRequestException => Format(Strings.ErrorNoConnection, options.Endpoint),
            IOException => Strings.ErrorConnectionLost,
            TimeoutException or OperationCanceledException => Strings.ErrorTimeout,
            _ => exception.Message,
        };
    }

    /// <summary>
    /// Whether "Continue" may help: not after errors the user has to fix first (the key, the model, the balance,
    /// settings).
    /// </summary>
    public static bool CanContinue(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is not AgentConfigurationException
            and not ClientResultException { Status: (int)HttpStatusCode.Unauthorized or (int)HttpStatusCode.PaymentRequired or (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.NotFound };
    }

    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.CurrentCulture, format, values);
}
