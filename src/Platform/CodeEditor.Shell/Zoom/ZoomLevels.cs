namespace CodeEditor.Shell.Zoom;

/// <summary>
/// Interface zoom levels in percent: 10 % steps from 50 % to 300 %. A value off the grid (115 from a hand-edited
/// <c>settings.json</c>) stays as is; the next step lands back on the grid.
/// </summary>
public static class ZoomLevels
{
    public const int Default = 100;
    public const int Min = 50;
    public const int Max = 300;
    public const int Step = 10;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, Max);

    /// <summary>Moves by whole steps on the grid: +1 from 115 is 120, -1 from 115 is 110; the result stays in range.</summary>
    public static int Move(int percent, int steps)
    {
        var current = Clamp(percent);
        if (steps == 0)
        {
            return current;
        }

        // Snap off-grid values towards the direction of travel, then take the remaining steps.
        var snapped = steps > 0 ? FloorToGrid(current) : CeilingToGrid(current);
        return Clamp(snapped + (steps * Step));
    }

    private static int FloorToGrid(int percent) => percent / Step * Step;

    private static int CeilingToGrid(int percent) => (percent + Step - 1) / Step * Step;
}
