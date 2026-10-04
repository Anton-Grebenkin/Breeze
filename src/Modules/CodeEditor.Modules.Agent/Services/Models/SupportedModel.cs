namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// A supported model (ADR 0017): prompts are tuned for it, the benchmark checks it, and its context window and response
/// limit are known exactly from the service catalog rather than guessed from the name.
/// </summary>
/// <param name="ContextWindow">Context window in tokens.</param>
/// <param name="MaxOutputTokens">The longest response the model can return.</param>
public sealed record SupportedModel(string Id, ModelTier Tier, int ContextWindow, int MaxOutputTokens);
