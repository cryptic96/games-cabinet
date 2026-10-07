namespace Cabinet.Domain.Layout;

/// <summary>
/// Decides how a stored picture sits in a box front and which stored size a cover asks for. Pictures are only ever
/// scaled down and shown whole, so the decision is a comparison of two shapes and of two widths.
/// </summary>
public static class ArtFitting
{
    /// <summary>The path stored pictures are served from, on the site's own origin.</summary>
    public const string RequestPath = "/art";

    /// <summary>How many times wider than its drawn width a stored picture should be, so it stays sharp on a dense screen.</summary>
    public const double VariantOversample = 1.5;

    private const double ExactTolerance = 0.01;

    /// <summary>
    /// Compares the shape of the picture with the shape of the box front. They are the same shape when the two
    /// width-to-height ratios differ by at most one percent of the picture's ratio; otherwise a picture that is relatively
    /// wider fills the width and one that is relatively narrower fills the height.
    /// </summary>
    /// <param name="boxWidthMm">The width of the box front.</param>
    /// <param name="boxHeightMm">The height of the box front.</param>
    /// <param name="artWidth">The width of the picture in pixels.</param>
    /// <param name="artHeight">The height of the picture in pixels.</param>
    public static ArtFit Fit(int boxWidthMm, int boxHeightMm, int artWidth, int artHeight)
    {
        if (boxWidthMm <= 0 || boxHeightMm <= 0 || artWidth <= 0 || artHeight <= 0)
        {
            return ArtFit.Exact;
        }

        var boxRatio = (double)boxWidthMm / boxHeightMm;
        var artRatio = (double)artWidth / artHeight;

        if (Math.Abs(boxRatio - artRatio) <= artRatio * ExactTolerance)
        {
            return ArtFit.Exact;
        }

        return artRatio > boxRatio ? ArtFit.Width : ArtFit.Height;
    }

    /// <summary>
    /// Picks the stored size a cover asks for: the narrowest that is at least as wide as the cover is drawn at its largest,
    /// times the oversample, and the widest stored size when none is wide enough. Returns null when the picture has no sizes.
    /// </summary>
    /// <param name="art">The stored picture.</param>
    /// <param name="coverWidthMm">The width of the cover in millimetres.</param>
    /// <param name="design">The section design the cover stands in.</param>
    public static ArtVariant? Pick(ArtImage art, int coverWidthMm, SectionDesign design)
    {
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(design);

        if (art.Variants.Count == 0)
        {
            return null;
        }

        var neededPixels = (int)Math.Ceiling(coverWidthMm * (double)design.LargestRenderedWidthPx * VariantOversample / design.RenderedWidthMm);

        return art.Variants
            .Where(variant => variant.Width >= neededPixels)
            .OrderBy(variant => variant.Width)
            .FirstOrDefault()
            ?? art.Variants.OrderByDescending(variant => variant.Width).First();
    }
}
