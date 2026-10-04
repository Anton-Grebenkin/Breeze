using CodeEditor.Modules.Diagrams.ViewModels;

namespace CodeEditor.Modules.Diagrams.Tests;

/// <summary>Preview zoom steps match the browser; a wheel value between steps moves to the nearest step.</summary>
public sealed class DiagramZoomTests
{
    [Theory]
    [InlineData(1, 1.1)]
    [InlineData(0.95, 1)]
    [InlineData(1.0000001, 1.1)]
    [InlineData(8, 8)]
    public void In_GoesToTheNextStep(double zoom, double expected) => Assert.Equal(expected, DiagramZoom.In(zoom));

    [Theory]
    [InlineData(1, 0.9)]
    [InlineData(1.05, 1)]
    [InlineData(0.1, 0.1)]
    public void Out_GoesToThePreviousStep(double zoom, double expected) => Assert.Equal(expected, DiagramZoom.Out(zoom));

    [Theory]
    [InlineData(0.01, 0.1)]
    [InlineData(20, 8)]
    [InlineData(double.NaN, 1)]
    public void Clamp_KeepsTheBounds(double zoom, double expected) => Assert.Equal(expected, DiagramZoom.Clamp(zoom));
}
