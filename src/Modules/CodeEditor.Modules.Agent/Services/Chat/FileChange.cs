namespace CodeEditor.Modules.Agent.Services.Chat;

/// <summary>A file the agent changed in the chat: text before the first edit and now (<c>null</c> when absent).</summary>
public sealed record FileChange(string Path, string RelativePath, string? Original, string? Current)
{
    public bool IsCreated => Original is null && Current is not null;

    public bool IsDeleted => Original is not null && Current is null;
}
