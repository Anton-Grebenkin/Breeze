namespace CodeEditor.Modules.Tools.Services;

/// <summary>How a tool script is started.</summary>
public enum ToolScriptKind
{
    /// <summary><c>.ps1</c> — PowerShell 7 if installed, otherwise Windows PowerShell.</summary>
    PowerShell,

    /// <summary><c>.mjs</c>, <c>.js</c> — Node.js.</summary>
    Node,

    /// <summary><c>.cs</c> — a .NET file-based app (<c>dotnet run file.cs</c>).</summary>
    CSharp,
}
