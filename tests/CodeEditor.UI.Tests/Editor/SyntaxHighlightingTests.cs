using CodeEditor.UI.Tests.Infrastructure;

namespace CodeEditor.UI.Tests.Editor;

/// <summary>
/// Highlighting on the real window (ADR 0036): the language is detected from an extensionless file name too
/// (<c>Dockerfile</c>) and named in the status bar; screenshots show the new definitions' colors in both themes.
/// </summary>
public sealed class SyntaxHighlightingTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly string _folder;

    public SyntaxHighlightingTests()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "CodeEditor.UI.Tests", Guid.NewGuid().ToString("N"), "app")).FullName;
        File.WriteAllText(Path.Combine(_folder, "Dockerfile"),
            "# build stage\nFROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\nARG CONFIG=Release\nENV PATH=\"/app:${PATH}\"\nRUN dotnet publish -c $CONFIG -o /out\n");
        File.WriteAllText(Path.Combine(_folder, "compose.yaml"),
            "# services\nservices:\n  web:\n    image: \"app:1.0\"\n    ports:\n      - 8080:80\n    restart: always\n");
        File.WriteAllText(Path.Combine(_folder, "deploy.sh"),
            "#!/usr/bin/env bash\n# deploy\nNAME=\"world\"\nif [ -n \"$NAME\" ]; then\n  echo \"Hello, ${NAME}\"\nfi\n");
    }

    public void Dispose() => AppSession.DeleteQuietly(Path.GetDirectoryName(_folder)!);

    [Fact]
    public void LanguageByFileName_AndColorsOfNewDefinitions()
    {
        using var session = AppSession.WithArguments(_folder);

        Open(session, "Dockerfile", "Dockerfile");
        session.SaveScreenshot("highlighting-dockerfile");
        Open(session, "compose.yaml", "YAML");
        Open(session, "deploy.sh", "Shell Script");
        session.SaveScreenshot("highlighting-shell");
    }

    private static void Open(AppSession session, string file, string language)
    {
        session.WaitFor("Explorer.Node." + file).GuardedDoubleClick();
        session.WaitFor("EditorTab." + file);
        session.WaitForText("StatusBar.editor.language", text => text == language, Timeout);
    }
}
