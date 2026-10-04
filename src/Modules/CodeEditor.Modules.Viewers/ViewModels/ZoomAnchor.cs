namespace CodeEditor.Modules.Viewers.ViewModels;

/// <summary>
/// Scroll offset after a zoom change: the image point under the cursor (wheel) or at the window center (buttons, keys)
/// stays in place. Computed per axis; an image smaller than the window is centered and does not scroll.
/// </summary>
public static class ZoomAnchor
{
    /// <param name="viewport">Visible area size.</param>
    /// <param name="pointer">Anchor point in the visible area: the cursor or the center.</param>
    /// <param name="offset">Scroll offset before the zoom change.</param>
    /// <param name="before">On-screen image size before the zoom change.</param>
    /// <param name="after">On-screen image size after it.</param>
    /// <returns>The new scroll offset, within the image.</returns>
    public static double Offset(double viewport, double pointer, double offset, double before, double after)
    {
        if (after <= viewport || before <= 0)
        {
            return 0;
        }

        var left = Math.Max(0, (viewport - before) / 2);
        var share = Math.Clamp((pointer + offset - left) / before, 0, 1);
        return Math.Clamp((share * after) - pointer, 0, after - viewport);
    }
}
