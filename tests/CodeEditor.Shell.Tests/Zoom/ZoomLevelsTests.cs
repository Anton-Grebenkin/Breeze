using CodeEditor.Shell.Zoom;

namespace CodeEditor.Shell.Tests.Zoom;

public sealed class ZoomLevelsTests
{
    [Theory]
    [InlineData(100, 1, 110)]
    [InlineData(100, -1, 90)]
    [InlineData(100, 3, 130)]
    [InlineData(115, 1, 120)]
    [InlineData(115, -1, 110)]
    [InlineData(300, 1, 300)]
    [InlineData(50, -1, 50)]
    [InlineData(1000, -1, 290)]
    [InlineData(10, 0, 50)]
    public void Move_StepsOnTenPercentGrid_WithinRange(int percent, int steps, int expected)
    {
        Assert.Equal(expected, ZoomLevels.Move(percent, steps));
    }

    [Fact]
    public void WheelSteps_CountNotches_AndAddUpTouchpadDeltas()
    {
        var wheel = new WheelSteps();

        int[] steps = [wheel.Add(120), wheel.Add(-240), wheel.Add(40), wheel.Add(40), wheel.Add(40), wheel.Add(360)];

        Assert.Equal([1, -2, 0, 0, 1, 3], steps);
    }

    [Fact]
    public void WheelSteps_DropRemainder_WhenTurnedBack()
    {
        var wheel = new WheelSteps();

        int[] steps = [wheel.Add(100), wheel.Add(-30), wheel.Add(-90)];

        Assert.Equal([0, 0, -1], steps);
    }
}
