namespace CodeEditor.Modules.Agent.Services.Hooks;

/// <summary>A <c>UserPromptSubmit</c> hook stopped the message; the text is for the user.</summary>
public sealed class HookBlockedException : Exception
{
    public HookBlockedException()
    {
    }

    public HookBlockedException(string message)
        : base(message)
    {
    }

    public HookBlockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
