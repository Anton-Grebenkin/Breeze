namespace CodeEditor.Modules.Agent.Rules;

/// <summary>Rules text for the system prompt and where it came from (path relative to the folder).</summary>
/// <param name="IsPersonal">From agent.md in the user data folder: applies to every project, folder rules win.</param>
public sealed record ProjectRules(string Source, string Text, bool IsPersonal = false);
