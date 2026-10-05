namespace Cabinet.Domain.Layout;

/// <summary>How a box is placed in its cubby.</summary>
public enum BoxPose
{
    /// <summary>Facing out so the whole front shows.</summary>
    Cover,

    /// <summary>Standing upright so only the spine shows.</summary>
    Spine,

    /// <summary>Lying flat with the spine facing out, in a short stack.</summary>
    Flat,
}

/// <summary>A coarse size of a box, taken from its standing height.</summary>
public enum SizeClass
{
    /// <summary>A short box.</summary>
    Small,

    /// <summary>A box of ordinary height.</summary>
    Standard,

    /// <summary>A tall box.</summary>
    Large,
}

/// <summary>
/// Decides how each game stands. A decision depends only on that game, the settings and the design, never on the other
/// games, so adding a game never changes how an existing game stands. The one exception is the few-games switch, which
/// looks at how many games there are and is accepted as a single global rearrangement.
/// </summary>
public static class Orientation
{
    /// <summary>Boxes lower than this stand as small ones, in millimetres.</summary>
    public const int SmallBelowHeightMm = 200;

    /// <summary>Boxes lower than this and not small stand as standard ones; taller boxes are large, in millimetres.</summary>
    public const int StandardBelowHeightMm = 320;

    private const int SmallWeight = 30;
    private const int StandardWeight = 100;
    private const int LargeWeight = 220;
    private const int MaxChanceBasisPoints = 9500;
    private const int BasisPoints = 10000;
    private const int BasisPointsPerPercent = 100;

    /// <summary>The size class of a box from its standing height.</summary>
    public static SizeClass SizeClassOf(BoxDimensions box)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.HeightMm < SmallBelowHeightMm)
        {
            return SizeClass.Small;
        }

        return box.HeightMm < StandardBelowHeightMm ? SizeClass.Standard : SizeClass.Large;
    }

    /// <summary>
    /// The chance, in basis points, that a box of the size class faces out under the size-weighted strategy: the share
    /// times a fixed weight for the class, capped so some boxes always stand. The weights are constants, so they never
    /// depend on the collection.
    /// </summary>
    public static int CoverChanceBasisPoints(int sharePercent, SizeClass sizeClass)
    {
        var weight = sizeClass switch
        {
            SizeClass.Small => SmallWeight,
            SizeClass.Standard => StandardWeight,
            _ => LargeWeight,
        };

        return Math.Min(MaxChanceBasisPoints, sharePercent * weight);
    }

    /// <summary>
    /// Decides whether the game faces out or stands as a spine. When <paramref name="fewGames"/> is true every box faces
    /// out. Otherwise the strategy picks the covers.
    /// </summary>
    public static BoxPose Decide(CabinetItem item, LayoutOptions options, SectionDesign design, bool fewGames)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(design);

        if (fewGames || FacesOut(item, options, design))
        {
            return BoxPose.Cover;
        }

        return BoxPose.Spine;
    }

    private static bool FacesOut(CabinetItem item, LayoutOptions options, SectionDesign design)
    {
        if (options.CoverStrategy == CoverStrategy.OversizeOnly)
        {
            return item.Box.HeightMm > design.MaxSpineHeightMm;
        }

        var chance = options.CoverStrategy == CoverStrategy.Random
            ? options.CoverSharePercent * BasisPointsPerPercent
            : CoverChanceBasisPoints(options.CoverSharePercent, SizeClassOf(item.Box));

        return StableHash.Bucket(item.BggId, StableHash.CoverSalt, BasisPoints) < chance;
    }
}
