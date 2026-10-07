using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>
/// Turns the box dimensions of an owned game version into the size drawn in the cabinet. Dimensions that are missing, zero
/// or not believable as a game box fall back to one realistic size per kind.
/// </summary>
public static class BoxFromVersion
{
    /// <summary>How many millimetres one unit of a reported dimension is; BoardGameGeek reports inches.</summary>
    public const double MillimetresPerUnit = 25.4;

    /// <summary>The shortest believable front side of a box, in millimetres.</summary>
    public const int MinFrontMm = 50;

    /// <summary>The longest believable front side of a box, in millimetres.</summary>
    public const int MaxFrontMm = 700;

    /// <summary>The thinnest believable box, in millimetres.</summary>
    public const int MinDepthMm = 5;

    /// <summary>The thickest believable box, in millimetres.</summary>
    public const int MaxDepthMm = 300;

    /// <summary>
    /// Maps a reported version to a box. The longer front side becomes the standing height and the shorter one the front
    /// width. Any part that is absent, not positive, or outside the believable range gives the default for the kind.
    /// </summary>
    /// <param name="dimensions">The reported dimensions, or null when the source gave none.</param>
    /// <param name="kind">Whether the item is a standalone game or an expansion.</param>
    public static BoxDimensions Map(VersionDimensions? dimensions, ItemKind kind) =>
        TryMap(dimensions) ?? DefaultFor(kind);

    /// <summary>
    /// Maps a reported version to a box as <see cref="Map"/> does, but says so with null when the dimensions are absent, not
    /// positive, or outside the believable range, instead of inventing a size.
    /// </summary>
    /// <param name="dimensions">The reported dimensions, or null when the source gave none.</param>
    public static BoxDimensions? TryMap(VersionDimensions? dimensions)
    {
        if (dimensions is null || !IsPositive(dimensions.Width) || !IsPositive(dimensions.Length) || !IsPositive(dimensions.Depth))
        {
            return null;
        }

        var first = ToMillimetres(dimensions.Width);
        var second = ToMillimetres(dimensions.Length);
        var depth = ToMillimetres(dimensions.Depth);
        var height = Math.Max(first, second);
        var width = Math.Min(first, second);

        return IsWithin(width, MinFrontMm, MaxFrontMm) && IsWithin(height, MinFrontMm, MaxFrontMm) && IsWithin(depth, MinDepthMm, MaxDepthMm)
            ? new BoxDimensions(width, height, depth)
            : null;
    }

    /// <summary>The box size used for an item whose real size is not known.</summary>
    /// <param name="kind">Whether the item is a standalone game or an expansion.</param>
    public static BoxDimensions DefaultFor(ItemKind kind) =>
        kind == ItemKind.Expansion ? new BoxDimensions(200, 260, 40) : new BoxDimensions(225, 300, 60);

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private static int ToMillimetres(double value) =>
        (int)Math.Round(Math.Min(value * MillimetresPerUnit, int.MaxValue / 2.0), MidpointRounding.AwayFromZero);

    private static bool IsWithin(int value, int minimum, int maximum) => value >= minimum && value <= maximum;
}
