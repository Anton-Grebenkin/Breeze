namespace CodeEditor.Shell.Integration;

/// <summary>For hosts without an installer (tests); the app replaces it.</summary>
public sealed class NoWindowsIntegration : IWindowsIntegration
{
    public bool IsAvailable => false;
}
