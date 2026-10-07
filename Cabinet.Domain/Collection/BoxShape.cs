using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>Where the drawn size of a box came from.</summary>
public enum BoxSource
{
    /// <summary>The dimensions of the owned version.</summary>
    RealSize,

    /// <summary>The shape of a flat cover, with a size taken from the dimensions or the estimate.</summary>
    CoverShape,

    /// <summary>An estimate from the game's weight, playing time and player count.</summary>
    Estimate,

    /// <summary>The default size for the kind, because nothing is known.</summary>
    Default,
}

/// <summary>A box to draw, with the height that decides how it stands and where its size came from.</summary>
/// <param name="Box">The size to draw.</param>
/// <param name="PoseHeightMm">The standing height used to decide how the box stands; it never comes from a picture.</param>
/// <param name="Source">Where the drawn size came from.</param>
public sealed record ShapedBox(BoxDimensions Box, int PoseHeightMm, BoxSource Source);

/// <summary>
/// Chooses the size to draw for a box: the real dimensions when they are believable, unless they clearly contradict a flat
/// cover, in which case the cover gives the shape; without real dimensions a flat cover gives the shape and an estimate the
/// size; without a cover the estimate or the default is drawn. A picture that is a photographed box or unsure never shapes
/// anything, so the caller passes a cover only for a flat one.
/// </summary>
public static class BoxShape
{
    private const double ShapeMarginTolerance = 1e-9;
    private const double PercentFactor = 100.0;

    /// <summary>
    /// Works out the box to draw and the height that decides how it stands. The pose height is the real height, else the
    /// estimated one, and never depends on the cover.
    /// </summary>
    /// <param name="item">The stored item with its reported dimensions.</param>
    /// <param name="details">What is known about the game, or null when nothing is.</param>
    /// <param name="flatCover">The stored picture file whose shape the box may take, or null when the picture is not a flat cover.</param>
    /// <param name="rules">The rules that set the disagreement margin and the orientation.</param>
    public static ShapedBox Resolve(SnapshotItem item, GameDetails? details, ArtFile? flatCover, ArtRules rules)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rules);

        var real = BoxFromVersion.TryMap(item.Dimensions);
        var estimate = EstimateOf(item, details);
        var poseHeight = real?.HeightMm ?? estimate.HeightMm;
        var cover = flatCover is { Width: > 0, Height: > 0 } ? flatCover : null;

        if (real is not null)
        {
            return cover is not null && Disagrees(real, cover, rules.ShapeMarginPercent)
                && Rebuilt(real, cover, rules) is { } rebuilt
                    ? new ShapedBox(rebuilt, poseHeight, BoxSource.CoverShape)
                    : new ShapedBox(real, poseHeight, BoxSource.RealSize);
        }

        if (cover is not null)
        {
            return new ShapedBox(FromCover(estimate, cover, rules), poseHeight, BoxSource.CoverShape);
        }

        return new ShapedBox(estimate, poseHeight, details is null ? BoxSource.Default : BoxSource.Estimate);
    }

    private static BoxDimensions EstimateOf(SnapshotItem item, GameDetails? details)
    {
        if (details is null)
        {
            return BoxFromVersion.DefaultFor(item.Kind);
        }

        var sizeClass = details.EstimatedSize is { } stored && details.EstimateModelVersion == SizeEstimate.ModelVersion
            ? stored
            : SizeEstimate.Assign(details, null, null);

        return sizeClass is { } known ? SizeEstimate.Dimensions(known, item.Kind) : BoxFromVersion.DefaultFor(item.Kind);
    }

    private static bool Disagrees(BoxDimensions real, ArtFile cover, int marginPercent)
    {
        var sizesRatio = (double)real.WidthMm / real.HeightMm;
        var coverRatio = RatioOf(cover);

        return Math.Abs(sizesRatio - coverRatio) / coverRatio * PercentFactor > marginPercent + ShapeMarginTolerance;
    }

    private static BoxDimensions? Rebuilt(BoxDimensions real, ArtFile cover, ArtRules rules)
    {
        var area = (double)real.WidthMm * real.HeightMm;
        var ratio = RatioOf(cover);
        var longer = (int)Math.Round(Math.Sqrt(area / ratio), MidpointRounding.AwayFromZero);
        var shorter = (int)Math.Round(longer * ratio, MidpointRounding.AwayFromZero);
        var front = Oriented(shorter, longer, cover, rules);

        return IsPlausible(front.WidthMm) && IsPlausible(front.HeightMm)
            ? new BoxDimensions(front.WidthMm, front.HeightMm, real.DepthMm)
            : null;
    }

    private static BoxDimensions FromCover(BoxDimensions estimate, ArtFile cover, ArtRules rules)
    {
        var longer = estimate.HeightMm;
        var shorter = (int)Math.Round(longer * RatioOf(cover), MidpointRounding.AwayFromZero);
        var front = Oriented(shorter, longer, cover, rules);

        return new BoxDimensions(front.WidthMm, front.HeightMm, estimate.DepthMm);
    }

    private static (int WidthMm, int HeightMm) Oriented(int shorter, int longer, ArtFile cover, ArtRules rules) =>
        rules.OrientFromCover && cover.Width > cover.Height ? (longer, shorter) : (shorter, longer);

    private static double RatioOf(ArtFile cover) =>
        (double)Math.Min(cover.Width, cover.Height) / Math.Max(cover.Width, cover.Height);

    private static bool IsPlausible(int frontSideMm) =>
        frontSideMm >= BoxFromVersion.MinFrontMm && frontSideMm <= BoxFromVersion.MaxFrontMm;
}
