using System.Globalization;
using CodeEditor.Modules.Viewers.Formats;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Image zoom, as in the VS Code image preview. Defaults to fit: a large image shrinks to fit the window, a small one is
/// not stretched (100 %); the zoom is refitted when the window resizes. Buttons and keys follow the VS Code steps, the
/// <c>Ctrl</c> wheel zooms smoothly; any explicit zoom turns fit off. Scale is screen pixels per image pixel: 100 % is
/// pixel for pixel.
/// </summary>
public sealed partial class ZoomState : ObservableObject
{
    public const double Min = 0.01;
    public const double Max = 20;

    /// <summary>From this scale the image is drawn without smoothing, showing pixels as in VS Code.</summary>
    public const double PixelationThreshold = 3;

    // A wheel zoom may sit between steps: the tolerance keeps 1.0000001 from counting as a step of its own.
    private const double Tolerance = 0.001;

    // One wheel notch (120 units) zooms by 1.2x.
    private const double WheelFactor = 1.2;
    private const double WheelNotch = 120;

    private static readonly double[] Steps = [0.02, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1, 1.5, 2, 3, 5, 7, 10, 15, 20];

    private PixelSize _content;
    private double _viewportWidth;
    private double _viewportHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(IsPixelated))]
    public partial double Scale { get; private set; } = 1;

    /// <summary>The zoom follows the window size.</summary>
    [ObservableProperty]
    public partial bool IsFit { get; private set; } = true;

    /// <summary>The zoom for the button: "50 %".</summary>
    public string Text => Scale.ToString("P0", CultureInfo.CurrentCulture);

    public bool IsPixelated => Scale >= PixelationThreshold - Tolerance;

    public static double Clamp(double scale) => double.IsFinite(scale) ? Math.Clamp(scale, Min, Max) : 1;

    /// <summary>Sets the image size in pixels and refits.</summary>
    public void SetContent(PixelSize size)
    {
        _content = size;
        Refit();
    }

    /// <summary>Sets the visible area size in screen pixels and refits.</summary>
    public void SetViewport(double width, double height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        Refit();
    }

    [RelayCommand]
    public void ZoomIn() => Choose(Steps.FirstOrDefault(step => step > Scale + Tolerance, Max));

    [RelayCommand]
    public void ZoomOut() => Choose(Steps.LastOrDefault(step => step < Scale - Tolerance, Min));

    /// <summary>100 %: one image pixel per screen pixel.</summary>
    [RelayCommand]
    public void ActualSize() => Choose(1);

    [RelayCommand]
    public void Fit()
    {
        IsFit = true;
        Refit();
    }

    /// <summary>The <c>Ctrl</c> wheel: <paramref name="delta"/> as in the wheel event, 120 per notch.</summary>
    public void Wheel(double delta) => Choose(Scale * Math.Pow(WheelFactor, delta / WheelNotch));

    /// <summary>A zoom chosen by the page (SVG drawing): wheel or fit.</summary>
    public void Report(double scale, bool isFit)
    {
        Scale = Clamp(scale);
        IsFit = isFit;
    }

    private void Choose(double scale)
    {
        IsFit = false;
        Scale = Clamp(scale);
    }

    private void Refit()
    {
        if (!IsFit || _content.IsEmpty || _viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        Scale = Clamp(Math.Min(1, Math.Min(_viewportWidth / _content.Width, _viewportHeight / _content.Height)));
    }
}
