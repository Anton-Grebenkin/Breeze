namespace CodeEditor.Modules.Agent.Services.Models;

/// <summary>
/// Tier of a supported model in the picker: fast (cheap, for simple tasks), optimal (everyday) or powerful (hard tasks).
/// </summary>
public enum ModelTier
{
    Fast,
    Optimal,
    Powerful,
}
