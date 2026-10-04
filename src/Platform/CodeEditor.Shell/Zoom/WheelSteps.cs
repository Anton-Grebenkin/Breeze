namespace CodeEditor.Shell.Zoom;

/// <summary>
/// Turns mouse wheel deltas into whole zoom steps. A wheel notch is <see cref="Notch"/>; a touchpad sends smaller
/// deltas that add up, so zoom doesn't race. Turning the other way drops what was left over.
/// </summary>
public sealed class WheelSteps
{
    /// <summary>The delta of one wheel notch (Windows <c>WHEEL_DELTA</c>).</summary>
    public const int Notch = 120;

    private int _remainder;

    /// <summary>Adds a delta and returns the steps it completes: positive when the wheel turns away from the user.</summary>
    public int Add(int delta)
    {
        if (Math.Sign(delta) != Math.Sign(_remainder))
        {
            _remainder = 0;
        }

        _remainder += delta;
        var steps = _remainder / Notch;
        _remainder -= steps * Notch;
        return steps;
    }
}
