namespace CodeEditor.Modules.Browser.Services;

/// <summary>Screenshot format: PNG keeps UI text sharp; JPEG when the PNG exceeds the model's limit.</summary>
public enum BrowserImageFormat
{
    Png,
    Jpeg,
}
