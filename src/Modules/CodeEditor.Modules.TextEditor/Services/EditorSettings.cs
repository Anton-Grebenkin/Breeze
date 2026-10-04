using System.Globalization;
using CodeEditor.Modules.TextEditor.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.TextEditor.Services;

/// <summary>
/// Editor appearance shared by all editors, from <c>editor.*</c> settings; updates live when <c>settings.json</c>
/// changes. Font zoom (<c>editor.fontZoom</c>, changed by <see cref="EditorFontZoom"/>) adds points to the font size.
/// </summary>
public sealed partial class EditorSettings : ObservableObject, IDisposable
{
    public const double DefaultFontSize = 14;
    public const double MinFontSize = 6;
    public const double MaxFontSize = 72;
    public const double ZoomStep = 1;

    private const int MaxTabSize = 16;

    private readonly IDisposable? _subscription;
    private double _baseFontSize = DefaultFontSize;

    public EditorSettings(IOptionsMonitor<EditorOptions> options)
    {
        Apply(options.CurrentValue);
        _subscription = options.OnChange(Apply);
    }

    [ObservableProperty]
    public partial double FontSize { get; private set; } = DefaultFontSize;

    /// <summary>Font from settings; <c>null</c> means the theme font.</summary>
    [ObservableProperty]
    public partial string? FontFamily { get; private set; }

    [ObservableProperty]
    public partial int TabSize { get; private set; } = 4;

    [ObservableProperty]
    public partial bool InsertSpaces { get; private set; } = true;

    [ObservableProperty]
    public partial bool WordWrap { get; private set; }

    [ObservableProperty]
    public partial bool ShowLineNumbers { get; private set; } = true;

    [ObservableProperty]
    public partial bool ShowWhitespace { get; private set; }

    /// <summary>Status bar text such as "Spaces: 4" or "Tab Size: 4", as in VS Code.</summary>
    public string IndentationText =>
        string.Format(CultureInfo.CurrentCulture, InsertSpaces ? Strings.IndentationSpaces : Strings.IndentationTabs, TabSize);

    /// <summary>Points the zoom adds to <c>editor.fontSize</c>; less than asked when the size hits its limit.</summary>
    public double FontZoom { get; private set; }

    /// <summary>Applies a font zoom at once, without waiting for <c>settings.json</c> to reload.</summary>
    public void SetFontZoom(double zoom)
    {
        FontSize = Math.Clamp(_baseFontSize + zoom, MinFontSize, MaxFontSize);
        FontZoom = FontSize - _baseFontSize;
    }

    public void Dispose() => _subscription?.Dispose();

    partial void OnTabSizeChanged(int value) => OnPropertyChanged(nameof(IndentationText));

    partial void OnInsertSpacesChanged(bool value) => OnPropertyChanged(nameof(IndentationText));

    // Invalid values don't break the editor: sizes are clamped, unknown words fall back to defaults.
    private void Apply(EditorOptions options)
    {
        _baseFontSize = Math.Clamp(options.FontSize, MinFontSize, MaxFontSize);
        FontFamily = string.IsNullOrWhiteSpace(options.FontFamily) ? null : options.FontFamily.Replace("'", string.Empty, StringComparison.Ordinal).Replace("\"", string.Empty, StringComparison.Ordinal);
        TabSize = Math.Clamp(options.TabSize, 1, MaxTabSize);
        InsertSpaces = options.InsertSpaces;
        WordWrap = Is(options.WordWrap, "on");
        ShowLineNumbers = !Is(options.LineNumbers, "off");
        ShowWhitespace = Is(options.RenderWhitespace, "all");
        SetFontZoom(options.FontZoom);
    }

    private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
