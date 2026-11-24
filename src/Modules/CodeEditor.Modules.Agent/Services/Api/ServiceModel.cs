namespace CodeEditor.Modules.Agent.Services.Api;

/// <summary>A model as its service lists it (<c>GET /models</c>), with what the service says about it, if anything.</summary>
/// <param name="ContextWindow">Context window in tokens; <c>null</c> when the service does not say.</param>
/// <param name="Images">The model sees images; <c>null</c> when the service does not say.</param>
public sealed record ServiceModel(string Id, int? ContextWindow = null, bool? Images = null);
