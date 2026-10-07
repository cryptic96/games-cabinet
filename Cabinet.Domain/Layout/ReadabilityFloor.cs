namespace Cabinet.Domain.Layout;

/// <summary>
/// Turns a size on screen into the matching size in the cabinet's own millimetres, so the smallest thing the cabinet
/// draws is never smaller than a person can read or tap. Whole numbers only.
/// </summary>
public static class ReadabilityFloor
{
    /// <summary>The least width or height, in screen pixels, of anything a visitor can tap.</summary>
    public const int TapTargetPx = 24;

    /// <summary>
    /// The height, in screen pixels, that one line of label text needs: one 12 pixel line at a line height of 1.2 takes
    /// 14.4 pixels, rounded up.
    /// </summary>
    public const int OneLineLabelPx = 15;

    /// <summary>The height, in screen pixels, that two lines of label text need.</summary>
    public const int TwoLineLabelPx = 36;

    /// <summary>The width, in screen pixels, that two side-by-side vertical lines of label text need.</summary>
    public const int TwoLineSpinePx = 29;

    /// <summary>
    /// The least size in millimetres that is drawn at least <paramref name="targetPx"/> pixels wide at the narrowest
    /// the section is ever shown: the pixel target divided by the smallest number of pixels per millimetre, rounded up so
    /// the drawn size never falls below the target.
    /// </summary>
    /// <param name="targetPx">The size wanted on screen, in pixels.</param>
    /// <param name="smallestRenderedWidthPx">The narrowest width, in pixels, the whole section is drawn at.</param>
    /// <param name="renderedWidthMm">The width in millimetres that the section's drawn width stands for.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is not positive.</exception>
    public static int Millimetres(int targetPx, int smallestRenderedWidthPx, int renderedWidthMm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(smallestRenderedWidthPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedWidthMm);

        var scaled = ((long)targetPx * renderedWidthMm) + smallestRenderedWidthPx - 1;

        return (int)(scaled / smallestRenderedWidthPx);
    }
}
